using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    // DuckDB.NET 1.4.1 appender cannot write STRUCT columns yet, so batch with VALUES instead.
    private const int InsertBatchSize = 512;

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
        Execute(connection, $"ATTACH '{attachPath}' AS project (BLOCK_SIZE {BlockSize});");
        Execute(connection, "USE project;");

        CreateInfrastructureTables(connection);
        foreach (var component in snapshot.Registry.Components)
            CreateComponentTable(connection, component);

        Execute(connection, "BEGIN TRANSACTION;");
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
        InsertMetadata(connection, "format", "ciallo-project-duckdb");
        InsertMetadata(connection, "format_version", FormatVersion.ToString());
        InsertMetadata(connection, "created_by", "Ciallo");
        InsertMetadata(connection, "ciallo_version", applicationVersion);
    }

    private static void CreateComponentTable(DuckDBConnection connection, ComponentDescriptor component)
    {
        var columns = new List<string> { "\"entity_id\" integer primary key not null" };
        foreach (var field in component.Fields)
            columns.Add($"{Quote(field.Name)} {field.DuckDbColumnType}");

        Execute(connection, $"create table {Quote(component.TableName)} ({string.Join(", ", columns)});");
    }

    private static void InsertMetadata(DuckDBConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """insert into "metadata" ("key", "value") values ($key, $value);""";
        command.Parameters.Add(new DuckDBParameter("key", key));
        command.Parameters.Add(new DuckDBParameter("value", value));
        command.ExecuteNonQuery();
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
        var batch = new List<InsertBuilder>(InsertBatchSize);
        foreach (var row in component.Rows)
        {
            batch.Add(BuildComponentRow(component.Descriptor, row, "r" + batch.Count));
            if (batch.Count == InsertBatchSize)
            {
                ExecuteComponentBatch(connection, component.Descriptor, batch);
                batch.Clear();
            }
        }

        ExecuteComponentBatch(connection, component.Descriptor, batch);
    }

    private static InsertBuilder BuildComponentRow(
        ComponentDescriptor descriptor,
        PersistenceComponentRow row,
        string parameterPrefix)
    {
        var builder = new InsertBuilder(parameterPrefix);
        builder.AddColumn("entity_id", builder.NextParam(row.EntityId));

        for (int i = 0; i < descriptor.Fields.Count; i++)
            SerializeCapturedField(builder, descriptor.Fields[i], row.Values[i]);

        return builder;
    }

    private static void ExecuteComponentBatch(
        DuckDBConnection connection,
        ComponentDescriptor descriptor,
        List<InsertBuilder> batch)
    {
        if (batch.Count == 0)
            return;

        using var command = connection.CreateCommand();
        var rows = batch.Select(builder => $"({builder.ValueSql()})");
        command.CommandText =
            $"insert into {Quote(descriptor.TableName)} ({batch[0].ColumnSql()}) values {string.Join(", ", rows)};";
        foreach (var builder in batch)
            builder.Apply(command);
        command.ExecuteNonQuery();
    }

    private static void SerializeCapturedField(
        InsertBuilder builder,
        FieldDescriptor field,
        object value)
    {
        switch (field.Shape)
        {
            case FieldShape.Scalar:
                builder.AddColumn(field.Name,
                    builder.NextParam(ScalarConvert.ToDb(field.NonNullableValueType, value)));
                break;

            case FieldShape.Struct:
                builder.AddColumn(field.Name, BuildStructExpr(builder, field, value));
                break;

            case FieldShape.StructArray:
                builder.AddColumn(field.Name, BuildStructArrayExpr(builder, field, value));
                break;

            case FieldShape.PrimitiveArray:
                var list = field.BuildDbList(value);
                builder.AddColumn(field.Name,
                    $"{builder.NextParam(list)}::{FieldDescriptor.DuckScalarType(field.ElementType)}[]");
                break;

            case FieldShape.EntityRef:
                builder.AddColumn(field.Name, builder.NextParam(value));
                break;

            case FieldShape.EntityArray:
                builder.AddColumn(field.Name, $"{builder.NextParam(value)}::INTEGER[]");
                break;

            case FieldShape.EntityMap:
                builder.AddColumn(field.Name, BuildEntityMapExpr(builder, (PersistenceEntityMap)value));
                break;

            case FieldShape.Blob:
                builder.AddColumn(field.Name, builder.NextParam(value));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(field.Shape), field.Shape, null);
        }
    }

    private static string BuildStructExpr(InsertBuilder builder, FieldDescriptor field, object value)
    {
        if (value == null)
            return "NULL";

        var leaves = new float[field.Codec.LeafCount];
        field.Codec.Decompose(value, leaves);
        var paramNames = new string[leaves.Length];
        for (int i = 0; i < leaves.Length; i++)
            paramNames[i] = builder.NextParam(leaves[i]);
        return field.Codec.Literal(i => paramNames[i]);
    }

    private static string BuildStructArrayExpr(InsertBuilder builder, FieldDescriptor field, object value)
    {
        var codec = field.Codec;
        int leafCount = codec.LeafCount;

        var leafLists = new List<float>[leafCount];
        for (int i = 0; i < leafCount; i++)
            leafLists[i] = new List<float>();

        codec.DecomposeInto(value, leafLists);

        // Bind one FLOAT[] per leaf, zip them positionally, then build each STRUCT from e[1..N].
        var zipArgs = leafLists.Select(ll => $"{builder.NextParam(ll)}::FLOAT[]");
        var zip = $"list_zip({string.Join(", ", zipArgs)})";
        var literal = codec.Literal(i => $"e[{i + 1}]");
        return $"list_transform({zip}, e -> {literal})";
    }

    private static string BuildEntityMapExpr(InsertBuilder builder, PersistenceEntityMap value)
    {
        if (value == null)
            return "NULL";
        return $"map({builder.NextParam(value.Keys)}::INTEGER[], {builder.NextParam(value.Values)}::INTEGER[])";
    }

    #endregion

    #region Read

    private static Entity ReadDatabase(string dbPath, ProjectFormatRegistry registry)
    {
        using var connection = new DuckDBConnection($"Data Source={dbPath};ACCESS_MODE=READ_ONLY");
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

/// <summary>
/// Accumulates one component row's columns, value expressions, and parameters. Value expressions
/// may be plain parameter refs ($p0) or composite SQL (struct_pack / list_transform) referencing
/// several parameters.
/// </summary>
internal sealed class InsertBuilder
{
    private readonly List<string> _columns = new();
    private readonly List<string> _valueExprs = new();
    private readonly List<DuckDBParameter> _parameters = new();
    private readonly string _parameterPrefix;
    private int _seq;

    public InsertBuilder(string parameterPrefix = "p")
    {
        _parameterPrefix = parameterPrefix;
    }

    public void AddColumn(string column, string valueExpr)
    {
        _columns.Add("\"" + column.Replace("\"", "\"\"") + "\"");
        _valueExprs.Add(valueExpr);
    }

    public string NextParam(object value)
    {
        var name = _parameterPrefix + "_" + _seq++;
        _parameters.Add(new DuckDBParameter(name, value ?? (object)DBNull.Value));
        return "$" + name;
    }

    public string ColumnSql() => string.Join(", ", _columns);
    public string ValueSql() => string.Join(", ", _valueExprs);

    public void Apply(DuckDBCommand command)
    {
        foreach (var parameter in _parameters)
            command.Parameters.Add(parameter);
    }
}
