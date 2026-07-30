using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DuckDB.NET.Data;
using Frent;
using Godot;
using MessagePack;

// Note: June 22, 2026. DuckDB-backed project format. A .ciallo file IS a DuckDB database file
// (no zip container). Each persisted Component Class becomes a "component_<Name>" table; each
// [ProjectField] becomes exactly one column. Creative values (Color, Vector2, Transform2D, Bezier
// curves, stroke geometry) are stored as DuckDB STRUCT / STRUCT[] so the file is inspectable via
// SQL. Entity references are INTEGER positional ids (document = 0). Binary media stays BLOB.

namespace Ciallo.Data;

public static class DuckDbProjectSerializer
{
    public const int FormatVersion = 1;

    // DuckDB storage block size for new project files (bytes, power of two, 16KB..256KB).
    private const int BlockSize = 65536;
    private const string StorageVersion = "v1.5.0";
    // DuckDB.NET.Data cannot write composite appender columns yet; DuckDbBatchAppender supplies
    // the same row-oriented boundary over the public DuckDB C bindings.

    #region Public API

    public static void Save(Entity document, string filePath)
    {
        // Detaching first (entity refs -> positional ids, mutable arrays copied) means both save
        // paths share one writer. The extra capture copy is acceptable for a user-initiated save,
        // and reference-resolution errors now surface here rather than mid-write.
        Save(PersistenceSnapshotCapture.Capture(document, 0), filePath);
    }

    public static void Save(PersistenceSnapshot snapshot, string filePath)
    {
        SaveStaged(filePath, stagingPath => WriteDatabase(snapshot, stagingPath));
    }

    private static void SaveStaged(string filePath, Action<string> writeStagingFile)
    {
        var targetPath = Path.GetFullPath(filePath);
        var targetDirectory = Path.GetDirectoryName(targetPath)
                              ?? throw new InvalidOperationException($"File {filePath} has no directory.");
        Directory.CreateDirectory(targetDirectory);

        // Write to a sibling temp file, then atomically replace the target so a crash mid-save
        // never corrupts an existing project.
        var stagingPath = Path.Combine(
            targetDirectory,
            "." + Path.GetFileName(targetPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            writeStagingFile(stagingPath);
            CommitSave(stagingPath, targetPath);
        }
        finally
        {
            TryDeleteFile(stagingPath);
            // If WriteDatabase threw before CHECKPOINT/DETACH, DuckDB may leave a WAL sidecar
            // (<dbfile>.wal) next to the staging file. Clean it up so a failed save leaves nothing.
            TryDeleteFile(stagingPath + ".wal");
        }
    }

    // Returns the loaded document entity.
    public static Entity Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File {filePath} not found.");

        var registry = ProjectFormatRegistry.Shared;
        var document = ReadDatabase(filePath, registry);
        var settings = document.Get<DocumentSetting>();
        if (settings.DocumentId.Value == Guid.Empty)
            throw new InvalidDataException("Project database has an invalid document identity.");
        settings.FilePath.Value = filePath;
        return document;
    }

    #endregion

    #region Commit / cleanup

    private static void CommitSave(string stagingPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(targetPath)!,
                "." + Path.GetFileName(targetPath) + "." + Guid.NewGuid().ToString("N") + ".bak");
            File.Replace(stagingPath, targetPath, backupPath);
            TryDeleteFile(backupPath);
            return;
        }

        File.Move(stagingPath, targetPath, false);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    #endregion

    #region Write

    private static void WriteDatabase(PersistenceSnapshot snapshot, string dbPath)
    {
        if (File.Exists(dbPath))
            File.Delete(dbPath);

        // BLOCK_SIZE can only be chosen at database creation, and only via ATTACH (a direct
        // "Data Source=file" connection always uses the 256KB default). DuckDB allocates storage
        // one block per column-segment at minimum, so the default block size wastes megabytes on
        // a project full of tiny component tables. 64KB cuts that waste ~4x for small/empty
        // documents while staying negligible for large stroke-geometry columns (which span many
        // blocks regardless). The block size lives in the file header, so files stay self-describing
        // and each save rewrites the file at the current block size.
        var attachPath = dbPath.Replace("'", "''");
        using var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection,
            $"ATTACH '{attachPath}' AS project " +
            $"(BLOCK_SIZE {BlockSize}, STORAGE_VERSION '{StorageVersion}', RECOVERY_MODE 'no_wal_writes');");
        Execute(connection, "USE project;");

        // One transaction for the whole write, DDL included: each CREATE TABLE would otherwise
        // auto-commit (and hit the WAL) on its own, so a document with dozens of component tables
        // pays dozens of extra commits before the first row is written. The appender reads table
        // metadata through the same client context, so tables created earlier in the transaction
        // are visible to it.
        Execute(connection, "BEGIN TRANSACTION;");
        CreateInfrastructureTables(connection);
        foreach (var component in snapshot.Registry.Components)
            CreateComponentTable(connection, component);

        WriteMetadata(connection, snapshot.ApplicationVersion);
        InsertEntities(connection, snapshot.EntityCount);
        foreach (var component in snapshot.Components)
            InsertComponentRows(connection, component);
        Execute(connection, "COMMIT;");

        // Collapse the WAL into the main file so the single .ciallo file is self-contained
        // and can be atomically moved with no sidecar.
        Execute(connection, "CHECKPOINT project;");
        Execute(connection, "USE memory;");
        Execute(connection, "DETACH project;");
    }

    private static void CreateInfrastructureTables(DuckDBConnection connection)
    {
        Execute(connection, """
                            create table "metadata" (
                                "key" varchar primary key not null,
                                "value" varchar not null
                            );
                            """);
        Execute(connection, """
                            create table "entities" (
                                "id" integer primary key not null
                            );
                            """);
    }

    private static void WriteMetadata(DuckDBConnection connection, string applicationVersion)
    {
        using var appender = connection.CreateBatchAppender("metadata");
        AppendMetadata(appender, "format", "ciallo-project-duckdb");
        AppendMetadata(appender, "format_version", FormatVersion.ToString());
        AppendMetadata(appender, "created_by", "Ciallo");
        AppendMetadata(appender, "ciallo_version", applicationVersion);
    }

    private static void CreateComponentTable(DuckDBConnection connection, ComponentDescriptor component)
    {
        var columns = new List<string> { "\"entity_id\" integer primary key not null" };
        foreach (var field in component.Fields)
            columns.Add($"{Quote(field.Name)} {field.DuckDbColumnType}");

        Execute(connection, $"create table {Quote(component.TableName)} ({string.Join(", ", columns)});");
    }

    private static void AppendMetadata(DuckDbBatchAppender appender, string key, string value)
    {
        var row = appender.CreateRow();
        row.AppendValue(key).AppendValue(value);
        row.EndRow();
    }

    private static void InsertEntities(DuckDBConnection connection, int count)
    {
        if (count <= 0)
            return;

        // Ids are a dense 0..count-1 sequence, so let DuckDB generate them in one statement
        // instead of binding a parameter per row. The count is our own integer (not user input),
        // so inlining it is injection-safe. range(stop) yields BIGINT 0..stop-1.
        Execute(connection,
            $"""insert into "entities" ("id") select cast("range" as integer) from range({count});""");
    }

    private static void InsertComponentRows(
        DuckDBConnection connection,
        PersistenceComponentSnapshot component)
    {
        using var appender = connection.CreateBatchAppender(component.Descriptor.TableName);
        foreach (var row in component.Rows)
        {
            var target = appender.CreateRow();
            target.AppendValue(row.EntityId);
            for (int i = 0; i < component.Descriptor.Fields.Count; i++)
                AppendCapturedField(target, component.Descriptor.Fields[i], row.Values[i]);
            target.EndRow();
        }
    }

    private static void AppendCapturedField(
        DuckDbBatchAppenderRow row,
        FieldDescriptor field,
        object value)
    {
        switch (field.Shape)
        {
            case FieldShape.Scalar:
                row.AppendValue(ScalarConvert.ToDb(field.NonNullableValueType, value));
                break;

            case FieldShape.Struct:
                if (value == null)
                {
                    row.AppendStruct(null);
                    break;
                }
                var leaves = new float[field.Codec.LeafCount];
                field.Codec.Decompose(value, leaves);
                row.AppendStruct(leaves);
                break;

            case FieldShape.StructArray:
                // Decompose straight into the STRUCT[] child leaf vectors: no per-row
                // List<float>[] buffers and no per-element boxing. This is the stroke-geometry
                // hot path (SampledPolyline Positions/Tilts).
                row.AppendStructList(field.Codec, value);
                break;

            case FieldShape.PrimitiveArray:
                AppendPrimitiveArray(row, field, value);
                break;

            case FieldShape.EntityRef:
                row.AppendValue(value);
                break;

            case FieldShape.EntityArray:
                row.AppendValue((IList)value);
                break;

            case FieldShape.EntityMap:
                var map = (PersistenceEntityMap)value;
                row.AppendMap(map?.Keys, map?.Values);
                break;

            case FieldShape.Blob:
                row.AppendValue((byte[])value);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(field.Shape), field.Shape, null);
        }
    }

    // Bulk-copy a primitive scalar array (FLOAT[]/INTEGER[]/...) straight into the list child
    // vector with no boxing. SampledPolyline Radii/Pressures ride this path; falls back to the
    // boxed IList overload for element types without a fast span path.
    private static void AppendPrimitiveArray(DuckDbBatchAppenderRow row, FieldDescriptor field, object value)
    {
        switch (value)
        {
            case null:
                row.AppendPrimitiveList(ReadOnlySpan<float>.Empty);
                break;
            case ImmutableArray<float> f:
                row.AppendPrimitiveList(f.IsDefault ? default : ImmutableCollectionsMarshal.AsArray(f).AsSpan());
                break;
            case float[] f:
                row.AppendPrimitiveList<float>(f);
                break;
            case ImmutableArray<int> i:
                row.AppendPrimitiveList(i.IsDefault ? default : ImmutableCollectionsMarshal.AsArray(i).AsSpan());
                break;
            case int[] i:
                row.AppendPrimitiveList<int>(i);
                break;
            case double[] d:
                row.AppendPrimitiveList<double>(d);
                break;
            case ImmutableArray<double> d:
                row.AppendPrimitiveList(d.IsDefault ? default : ImmutableCollectionsMarshal.AsArray(d).AsSpan());
                break;
            default:
                row.AppendValue(FieldDescriptor.EnumerateArray(value).ToList());
                break;
        }
    }

    #endregion

    #region Read

    private static Entity ReadDatabase(string dbPath, ProjectFormatRegistry registry)
    {
        var connectionString = new DuckDBConnectionStringBuilder { DataSource = dbPath };
        // For security
        connectionString["ACCESS_MODE"] = "READ_ONLY";
        connectionString["enable_external_access"] = false;
        connectionString["autoinstall_known_extensions"] = false;
        connectionString["autoload_known_extensions"] = false;
        connectionString["allow_community_extensions"] = false;
        connectionString["lock_configuration"] = true;

        using var connection = new DuckDBConnection(connectionString.ConnectionString);
        connection.Open();

        var ids = ReadEntityIds(connection);
        if (!ids.Contains(0))
            throw new InvalidOperationException("Project database has no document entity id 0.");

        var world = new World();
        var idToEntity = new Dictionary<int, Entity>();
        foreach (var id in ids.OrderBy(i => i))
        {
            var entity = world.Create();
            entity.Tag<ToSerializeTag>();
            idToEntity[id] = entity;
        }

        // Deduplicate strings within a single load so identical values (e.g. layer names repeated
        // across thousands of rows) share one reference.
        var stringPool = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var component in registry.Components)
            ReadComponentTable(connection, component, idToEntity, stringPool);

        return idToEntity[0];
    }

    private static List<int> ReadEntityIds(DuckDBConnection connection)
    {
        var result = new List<int>();
        using var command = connection.CreateCommand();
        command.CommandText = """select "id" from "entities";""";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(Convert.ToInt32(reader.GetValue(0)));
        return result;
    }

    private static void ReadComponentTable(
        DuckDBConnection connection,
        ComponentDescriptor component,
        Dictionary<int, Entity> idToEntity,
        Dictionary<string, string> stringPool)
    {
        if (!TableExists(connection, component.TableName))
            return;

        // Pure numeric-array tables (stroke geometry) can read straight from data-chunk vectors,
        // with no per-element Dictionary or boxed float. This is a static, query-free filter: only
        // such tables pay for the column-name lookup and native type check below. Everything else
        // (the majority of components) goes straight to the managed reader, which reads actual
        // column names from the DbDataReader itself.
        if (NativeComponentReader.IsEligible(component.Fields)
            && TryReadNative(connection, component, idToEntity))
            return;

        using var command = connection.CreateCommand();
        command.CommandText = $"select * from {Quote(component.TableName)};";
        using var reader = command.ExecuteReader();

        // Map present column names so a schema that gained/lost a field since save is tolerated.
        var present = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < reader.FieldCount; i++)
            present[reader.GetName(i)] = i;

        if (!present.TryGetValue("entity_id", out var entityIdOrdinal))
            throw new InvalidOperationException($"{component.TableName} has no entity_id column.");

        while (reader.Read())
        {
            var entityId = Convert.ToInt32(reader.GetValue(entityIdOrdinal));
            if (!idToEntity.TryGetValue(entityId, out var entity))
                throw new InvalidOperationException($"{component.TableName}.entity_id {entityId} is not in entities.");

            var instance = Activator.CreateInstance(component.ComponentType)
                           ?? throw new InvalidOperationException($"Cannot create {component.ComponentType}.");

            foreach (var field in component.Fields)
            {
                if (!present.TryGetValue(field.Name, out var ordinal))
                    continue;

                var dbValue = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
                DeserializeField(field, instance, dbValue, idToEntity, stringPool);
            }

            entity.AddAs(component.ComponentType, instance);
        }
    }

    // Try the native chunk reader for an eligible table. Reads column names (the only query this
    // adds, and only for native candidates) to require the full current schema: if the file gained
    // or lost a field since save, or the on-disk types don't match the codecs, returns false so the
    // caller falls back to the managed reader, which tolerates those differences.
    private static bool TryReadNative(
        DuckDBConnection connection,
        ComponentDescriptor component,
        Dictionary<int, Entity> idToEntity)
    {
        var columnNames = ReadColumnNames(connection, component.TableName);
        if (!columnNames.Contains("entity_id"))
            throw new InvalidOperationException($"{component.TableName} has no entity_id column.");

        // The native reader materializes every field; if any column is missing, defer to the
        // managed reader's per-field tolerance instead.
        if (!component.Fields.All(f => columnNames.Contains(f.Name)))
            return false;

        return NativeComponentReader.TryRead(connection, component, component.Fields, idToEntity);
    }

    private static void DeserializeField(
        FieldDescriptor field,
        object instance,
        object dbValue,
        Dictionary<int, Entity> idToEntity,
        Dictionary<string, string> stringPool)
    {
        switch (field.Shape)
        {
            case FieldShape.Scalar:
                {
                    var value = ScalarConvert.FromDb(field.NonNullableValueType, dbValue);
                    if (value is string s)
                        value = InternString(stringPool, s);
                    field.SetProjectValue(instance, value);
                    break;
                }

            case FieldShape.Struct:
                {
                    var value = dbValue == null ? null : field.Codec.Compose(AsDict(dbValue));
                    field.SetProjectValue(instance, value);
                    break;
                }

            case FieldShape.StructArray:
                {
                    var rows = dbValue as IEnumerable ?? Array.Empty<object>();
                    field.SetProjectValue(instance, field.Codec.ComposeArray(rows, field.ContainerKind));
                    break;
                }

            case FieldShape.PrimitiveArray:
                {
                    var elements = dbValue == null
                        ? new List<object>()
                        : ((IEnumerable)dbValue).Cast<object>()
                            .Select(o => ScalarConvert.FromDb(field.ElementType, o)).ToList();
                    field.SetProjectValue(instance, ContainerFactory.Build(field.ContainerKind, field.ElementType, elements));
                    break;
                }

            case FieldShape.EntityRef:
                {
                    var entity = dbValue == null ? Entity.Null : ResolveEntity(idToEntity, dbValue, field);
                    field.SetProjectValue(instance, entity);
                    break;
                }

            case FieldShape.EntityArray:
                {
                    var entities = dbValue == null
                        ? new List<Entity>()
                        : ((IEnumerable)dbValue).Cast<object>().Select(o => ResolveEntity(idToEntity, o, field)).ToList();
                    PopulateEntityArray(field, instance, entities);
                    break;
                }

            case FieldShape.EntityMap:
                {
                    if (dbValue == null)
                        break; // null map: leave the field at its constructor default (e.g. not a cel folder)
                    PopulateEntityMap(field, instance, (IDictionary)dbValue, idToEntity);
                    break;
                }

            case FieldShape.Blob:
                {
                    var value = dbValue == null
                        ? null
                        : MessagePackSerializer.Deserialize(field.ValueType, BlobBytes(dbValue));
                    field.SetProjectValue(instance, value);
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(field.Shape), field.Shape, null);
        }
    }

    private static void PopulateEntityArray(FieldDescriptor field, object instance, List<Entity> entities)
    {
        var existing = field.GetFieldStorageObject(instance);
        if (existing != null)
        {
            ContainerFactory.PopulateEntityCollection(existing, entities);
            return;
        }

        var collection = ContainerFactory.Build(field.ContainerKind, typeof(Entity), entities.Cast<object>().ToList());
        field.SetFieldStorageObject(instance, collection);
    }

    private static void PopulateEntityMap(
        FieldDescriptor field, object instance, IDictionary map, Dictionary<int, Entity> idToEntity)
    {
        var mapInstance = field.GetFieldStorageObject(instance)
                          ?? Activator.CreateInstance(field.FieldType)
                          ?? throw new InvalidOperationException($"Cannot create {field.FieldType}.");

        var addMethod = field.FieldType.GetMethod("Add", [typeof(int), typeof(Entity)])
                        ?? throw new InvalidOperationException($"{field.FieldType} has no Add(int, Entity).");

        foreach (DictionaryEntry entry in map)
        {
            var key = Convert.ToInt32(entry.Key);
            var entity = ResolveEntity(idToEntity, entry.Value, field);
            addMethod.Invoke(mapInstance, [key, entity]);
        }

        field.SetFieldStorageObject(instance, mapInstance);
    }

    private static Entity ResolveEntity(Dictionary<int, Entity> idToEntity, object dbValue, FieldDescriptor field)
    {
        var id = Convert.ToInt32(dbValue);
        if (!idToEntity.TryGetValue(id, out var entity))
            throw new InvalidOperationException($"{field.Component.Name}.{field.Name} references missing entity id {id}.");
        return entity;
    }

    private static IReadOnlyDictionary<string, object> AsDict(object structValue)
    {
        if (structValue is IReadOnlyDictionary<string, object> readOnly)
            return readOnly;
        if (structValue is IDictionary<string, object> dict)
            return new Dictionary<string, object>(dict);

        // Fallback for non-generic dictionaries.
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in (IDictionary)structValue)
            result[Convert.ToString(entry.Key)!] = entry.Value;
        return result;
    }

    private static string InternString(Dictionary<string, string> pool, string s)
    {
        if (s.Length == 0)
            return string.Empty;
        if (pool.TryGetValue(s, out var existing))
            return existing;
        pool[s] = s;
        return s;
    }

    #endregion

    #region Utilities

    private static bool TableExists(DuckDBConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "select 1 from information_schema.tables where table_name = $name;";
        command.Parameters.Add(new DuckDBParameter("name", tableName));
        return command.ExecuteScalar() != null;
    }

    private static HashSet<string> ReadColumnNames(DuckDBConnection connection, string tableName)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText =
            "select column_name from information_schema.columns where table_name = $name;";
        command.Parameters.Add(new DuckDBParameter("name", tableName));
        using var reader = command.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    private static void Execute(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    /// <summary>
    /// DuckDB.NET returns BLOB columns as a (read-only, unmanaged) Stream, not byte[].
    /// Materialize it into a byte[] for MessagePack.
    /// </summary>
    private static byte[] BlobBytes(object dbValue)
    {
        if (dbValue is byte[] bytes)
            return bytes;
        if (dbValue is Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        throw new InvalidOperationException($"Unexpected BLOB value type {dbValue.GetType()}.");
    }

    #endregion
}
