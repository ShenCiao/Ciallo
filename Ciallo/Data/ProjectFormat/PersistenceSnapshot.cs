using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Frent;
using Godot;
using MessagePack;

namespace Ciallo.Data;

public sealed class PersistenceSnapshot
{
    internal ProjectFormatRegistry Registry { get; }
    internal IReadOnlyList<PersistenceComponentSnapshot> Components { get; }

    public int EntityCount { get; }
    public Guid DocumentId { get; }
    public long PersistenceEpoch { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public string ApplicationVersion { get; }

    internal PersistenceSnapshot(
        ProjectFormatRegistry registry,
        IReadOnlyList<PersistenceComponentSnapshot> components,
        int entityCount,
        Guid documentId,
        long persistenceEpoch,
        DateTimeOffset capturedAtUtc,
        string applicationVersion)
    {
        Registry = registry;
        Components = components;
        EntityCount = entityCount;
        DocumentId = documentId;
        PersistenceEpoch = persistenceEpoch;
        CapturedAtUtc = capturedAtUtc;
        ApplicationVersion = applicationVersion;
    }
}

internal sealed record PersistenceComponentSnapshot(
    ComponentDescriptor Descriptor,
    IReadOnlyList<PersistenceComponentRow> Rows);

internal sealed record PersistenceComponentRow(int EntityId, object[] Values);

internal sealed record PersistenceEntityMap(int[] Keys, int[] Values);

public static class PersistenceSnapshotCapture
{
    private static readonly ConditionalWeakTable<GodotObject, BlobCacheEntry> BlobCache = new();

    public static PersistenceSnapshot Capture(Entity document, long persistenceEpoch)
    {
        var registry = ProjectFormatRegistry.Shared;
        var entities = BuildEntityList(document);
        var entityToId = new Dictionary<Entity, int>(entities.Count);
        for (int i = 0; i < entities.Count; i++)
            entityToId.Add(entities[i], i);

        var componentSnapshots = new List<PersistenceComponentSnapshot>(registry.Components.Count);
        foreach (var descriptor in registry.Components)
        {
            var rows = new List<PersistenceComponentRow>();
            for (int entityId = 0; entityId < entities.Count; entityId++)
            {
                var entity = entities[entityId];
                if (!entity.Has(descriptor.ComponentType))
                    continue;

                var component = entity.Get(descriptor.ComponentType);
                var values = new object[descriptor.Fields.Count];
                for (int fieldIndex = 0; fieldIndex < descriptor.Fields.Count; fieldIndex++)
                {
                    var field = descriptor.Fields[fieldIndex];
                    values[fieldIndex] = CaptureField(field, field.GetProjectValue(component), entityToId);
                }
                rows.Add(new PersistenceComponentRow(entityId, values));
            }
            componentSnapshots.Add(new PersistenceComponentSnapshot(descriptor, rows));
        }

        return new PersistenceSnapshot(
            registry,
            componentSnapshots,
            entities.Count,
            document.Get<DocumentSetting>().DocumentId.Value,
            persistenceEpoch,
            DateTimeOffset.UtcNow,
            ProjectSettings.GetSetting("application/config/version", "unknown").AsString());
    }

    private static object CaptureField(
        FieldDescriptor field,
        object value,
        IReadOnlyDictionary<Entity, int> entityToId)
    {
        return field.Shape switch
        {
            FieldShape.Scalar or FieldShape.Struct => value,
            FieldShape.StructArray or FieldShape.PrimitiveArray => CaptureValueArray(field, value),
            FieldShape.EntityRef => CaptureEntityReference(field, value, entityToId),
            FieldShape.EntityArray => CaptureEntityArray(field, value, entityToId),
            FieldShape.EntityMap => CaptureEntityMap(field, value, entityToId),
            FieldShape.Blob => CaptureBlob(field, value),
            _ => throw new ArgumentOutOfRangeException(nameof(field.Shape), field.Shape, null),
        };
    }

    private static object CaptureValueArray(FieldDescriptor field, object value)
    {
        if (value == null || field.ContainerKind == ContainerKind.ImmutableArray)
            return value;

        var items = new List<object>();
        foreach (var item in FieldDescriptor.EnumerateArray(value))
            items.Add(item);

        var copy = Array.CreateInstance(field.ElementType, items.Count);
        for (int i = 0; i < items.Count; i++)
            copy.SetValue(items[i], i);
        return copy;
    }

    private static object CaptureEntityReference(
        FieldDescriptor field,
        object value,
        IReadOnlyDictionary<Entity, int> entityToId)
    {
        if (value == null)
            return null;

        var entity = (Entity)value;
        if (entity.IsNull)
        {
            if (field.EntityNullability == EntityNullability.Required)
                throw new InvalidOperationException(
                    $"Field {field.Component.Name}.{field.Name} is required but is Entity.Null.");
            return null;
        }

        return ResolveEntityId(field, entity, entityToId);
    }

    private static int[] CaptureEntityArray(
        FieldDescriptor field,
        object value,
        IReadOnlyDictionary<Entity, int> entityToId)
    {
        if (value == null)
            return [];

        var result = new List<int>();
        foreach (var entity in (IEnumerable<Entity>)value)
            result.Add(ResolveEntityId(field, entity, entityToId));
        return result.ToArray();
    }

    private static PersistenceEntityMap CaptureEntityMap(
        FieldDescriptor field,
        object value,
        IReadOnlyDictionary<Entity, int> entityToId)
    {
        if (value == null)
            return null;

        var keys = new List<int>();
        var values = new List<int>();
        foreach (var entry in (IEnumerable<KeyValuePair<int, Entity>>)value)
        {
            keys.Add(entry.Key);
            values.Add(ResolveEntityId(field, entry.Value, entityToId));
        }
        return new PersistenceEntityMap(keys.ToArray(), values.ToArray());
    }

    private static int ResolveEntityId(
        FieldDescriptor field,
        Entity entity,
        IReadOnlyDictionary<Entity, int> entityToId)
    {
        if (entity.IsNull)
            throw new InvalidOperationException(
                $"Collection field {field.Component.Name}.{field.Name} contains Entity.Null.");
        if (!entityToId.TryGetValue(entity, out var id))
            throw new InvalidOperationException(
                $"Field {field.Component.Name}.{field.Name} references an entity that is not persisted.");
        return id;
    }

    private static byte[] CaptureBlob(FieldDescriptor field, object value)
    {
        if (value == null)
            return null;
        if (value is not GodotObject godotObject)
            return MessagePackSerializer.Serialize(field.ValueType, value);

        var entry = BlobCache.GetOrCreateValue(godotObject);
        if (!entry.BytesByDeclaredType.TryGetValue(field.ValueType, out var bytes))
        {
            bytes = MessagePackSerializer.Serialize(field.ValueType, value);
            entry.BytesByDeclaredType.Add(field.ValueType, bytes);
        }
        return bytes;
    }

    private static List<Entity> BuildEntityList(Entity document)
    {
        var result = new List<Entity> { document };
        var query = document.World.CreateQuery().Tagged<ToSerializeTag>().Build();
        foreach (var entity in query.EnumerateWithEntities())
        {
            if (entity != document)
                result.Add(entity);
        }
        return result;
    }

    private sealed class BlobCacheEntry
    {
        public readonly Dictionary<Type, byte[]> BytesByDeclaredType = [];
    }
}
