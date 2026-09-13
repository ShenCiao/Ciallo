using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DuckDB.NET.Data;
using DuckDB.NET.Native;
using Frent;

namespace Ciallo.Data;

/// <summary>
/// Reads a component table straight from DuckDB data chunks, composing array columns from the
/// child vectors' native FLOAT/INTEGER buffers with no per-element boxing and no per-element
/// Dictionary. This is the read-side mirror of <see cref="DuckDbBatchAppender"/> and the hot path
/// for stroke geometry (SampledPolyline: Positions/Tilts STRUCT(x,y)[], Radii/Pressures FLOAT[]).
///
/// Only tables whose every persisted field is a numeric StructArray or a float/int PrimitiveArray
/// are eligible; anything with strings, blobs, entity refs, maps or scalars falls back to the
/// managed reader, which handles those shapes correctly.
/// </summary>
internal static unsafe class NativeComponentReader
{
    /// <summary>Whether <paramref name="presentFields"/> can all be read through the native path.</summary>
    public static bool IsEligible(IReadOnlyList<FieldDescriptor> presentFields)
    {
        if (presentFields.Count == 0)
            return false;

        foreach (var field in presentFields)
        {
            switch (field.Shape)
            {
                // Every StructCodec flattens to FLOAT leaves, so any StructArray reads as floats.
                case FieldShape.StructArray:
                    continue;
                case FieldShape.PrimitiveArray when field.ElementType == typeof(float)
                                                    || field.ElementType == typeof(int):
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }

    private sealed class ColumnPlan
    {
        public FieldDescriptor Field;
        public int Ordinal;            // column position in the SELECT list
        public int[][] LeafPaths;      // StructArray: struct-child index paths, one per FLOAT leaf
        public nint[] LeafData;        // StructArray: per-chunk FLOAT* for each leaf (reused)
        public nint ChildData;         // PrimitiveArray: per-chunk element buffer pointer
        public nint Entries;           // per-chunk DuckDBListEntry* of the list column
        public nint Validity;          // per-chunk validity mask (may be zero == all valid)
    }

    /// <summary>
    /// Reads the table through the native path. Returns false without touching any entity when the
    /// on-disk column types do not match what the codecs expect (an old file saved under a different
    /// schema, or a hand-edited database): the caller then falls back to the managed reader, which
    /// converts across types safely. This keeps the native path a pure optimization for the common
    /// case where the file was written by the current format.
    /// </summary>
    public static bool TryRead(
        DuckDBConnection connection,
        ComponentDescriptor component,
        IReadOnlyList<FieldDescriptor> presentFields,
        Dictionary<int, Entity> idToEntity)
    {
        var plans = new ColumnPlan[presentFields.Count];
        for (int i = 0; i < presentFields.Count; i++)
            plans[i] = new ColumnPlan { Field = presentFields[i], Ordinal = i + 1 };

        // entity_id first, then each present field, so ordinals are deterministic.
        var columns = "\"entity_id\"" + string.Concat(plans.Select(p => ", " + Quote(p.Field.Name)));
        var sql = $"select {columns} from {Quote(component.TableName)};";

        var state = NativeMethods.Query.DuckDBQuery(connection.NativeConnection, sql, out var result);
        try
        {
            if (!state.IsSuccess())
                throw new InvalidOperationException(
                    $"Reading {component.TableName}: {NativeMethods.Query.DuckDBResultError(ref result)}");

            // entity_id (column 0) must be a plain INTEGER, and every array column's on-disk type
            // must match its codec, or we cannot safely reinterpret the raw buffers.
            using (var idType = NativeMethods.Query.DuckDBColumnLogicalType(ref result, 0))
            {
                if (NativeMethods.LogicalType.DuckDBGetTypeId(idType) != DuckDBType.Integer)
                    return false;
            }
            if (!TryBindColumnTypes(ref result, plans))
                return false;

            ReadChunks(ref result, component, plans, idToEntity);
            return true;
        }
        finally
        {
            NativeMethods.Query.DuckDBDestroyResult(ref result);
        }
    }

    /// <summary>
    /// Validate each array column's on-disk logical type against its field's codec and record the
    /// StructArray leaf paths (stable across chunks). Returns false on any mismatch so the caller
    /// can fall back to the managed reader.
    /// </summary>
    private static bool TryBindColumnTypes(ref DuckDBResult result, ColumnPlan[] plans)
    {
        foreach (var plan in plans)
        {
            using var columnType = NativeMethods.Query.DuckDBColumnLogicalType(ref result, plan.Ordinal);
            if (NativeMethods.LogicalType.DuckDBGetTypeId(columnType) != DuckDBType.List)
                return false;

            using var childType = NativeMethods.LogicalType.DuckDBListTypeChildType(columnType);
            var childTypeId = NativeMethods.LogicalType.DuckDBGetTypeId(childType);

            if (plan.Field.Shape == FieldShape.StructArray)
            {
                if (childTypeId != DuckDBType.Struct)
                    return false;
                // A codec flattens to a fixed number of FLOAT leaves; a differing leaf count means
                // the stored STRUCT shape is not the one this codec decomposes/composes.
                plan.LeafPaths = DuckDbVectorWriter.BuildStructLeafPaths(columnType);
                if (plan.LeafPaths.Length != plan.Field.Codec.LeafCount)
                    return false;
                plan.LeafData = new nint[plan.LeafPaths.Length];
            }
            else // PrimitiveArray: element buffer is reinterpreted as float*/int*, so the width must match.
            {
                var expected = plan.Field.ElementType == typeof(float) ? DuckDBType.Float : DuckDBType.Integer;
                if (childTypeId != expected)
                    return false;
            }
        }
        return true;
    }

    private static void ReadChunks(
        ref DuckDBResult result,
        ComponentDescriptor component,
        ColumnPlan[] plans,
        Dictionary<int, Entity> idToEntity)
    {
        while (true)
        {
            var chunk = NativeMethods.Query.DuckDBFetchChunk(result);
            if (chunk.IsInvalid)
            {
                chunk.Dispose();
                break;
            }

            using (chunk)
            {
                var size = NativeMethods.DataChunks.DuckDBDataChunkGetSize(chunk);
                if (size == 0)
                    break;

                var idData = (int*)NativeMethods.Vectors.DuckDBVectorGetData(
                    NativeMethods.DataChunks.DuckDBDataChunkGetVector(chunk, 0));

                BindChunkVectors(chunk, plans);
                MaterializeRows(component, plans, idToEntity, idData, size);
            }
        }
    }

    // Refresh each column's per-chunk vector pointers (child buffers can move between chunks).
    private static void BindChunkVectors(DuckDBDataChunk chunk, ColumnPlan[] plans)
    {
        foreach (var plan in plans)
        {
            var listVector = NativeMethods.DataChunks.DuckDBDataChunkGetVector(chunk, plan.Ordinal);
            plan.Entries = (nint)NativeMethods.Vectors.DuckDBVectorGetData(listVector);
            plan.Validity = (nint)NativeMethods.Vectors.DuckDBVectorGetValidity(listVector);

            var child = NativeMethods.Vectors.DuckDBListVectorGetChild(listVector);
            if (plan.Field.Shape == FieldShape.StructArray)
            {
                for (int leaf = 0; leaf < plan.LeafPaths.Length; leaf++)
                    plan.LeafData[leaf] = (nint)NativeMethods.Vectors.DuckDBVectorGetData(
                        DuckDbVectorWriter.GetStructLeafVector(child, plan.LeafPaths[leaf]));
            }
            else
            {
                plan.ChildData = (nint)NativeMethods.Vectors.DuckDBVectorGetData(child);
            }
        }
    }

    private static void MaterializeRows(
        ComponentDescriptor component,
        ColumnPlan[] plans,
        Dictionary<int, Entity> idToEntity,
        int* idData,
        ulong size)
    {
        for (ulong row = 0; row < size; row++)
        {
            var entityId = idData[row];
            if (!idToEntity.TryGetValue(entityId, out var entity))
                throw new InvalidOperationException(
                    $"{component.TableName}.entity_id {entityId} is not in entities.");

            var instance = Activator.CreateInstance(component.ComponentType)
                           ?? throw new InvalidOperationException($"Cannot create {component.ComponentType}.");

            foreach (var plan in plans)
            {
                var entry = ((DuckDBListEntry*)plan.Entries)[row];
                // A null (invalid) list matches the managed reader's null-to-empty-container rule.
                var count = IsValid((ulong*)plan.Validity, row) ? (int)entry.Length : 0;

                object container = plan.Field.Shape == FieldShape.StructArray
                    ? plan.Field.Codec.ComposeArrayFromLeaves(
                        plan.LeafData, entry.Offset, count, plan.Field.ContainerKind)
                    : BuildPrimitiveContainer(plan, entry.Offset, count);

                plan.Field.SetProjectValue(instance, container);
            }

            entity.AddAs(component.ComponentType, instance);
        }
    }

    private static object BuildPrimitiveContainer(ColumnPlan plan, ulong offset, int count)
    {
        var elementType = plan.Field.ElementType;
        if (elementType == typeof(float))
            return BuildImmutableThenContainer<float>((float*)plan.ChildData, offset, count, plan.Field.ContainerKind);
        if (elementType == typeof(int))
            return BuildImmutableThenContainer<int>((int*)plan.ChildData, offset, count, plan.Field.ContainerKind);
        throw new InvalidOperationException($"Unsupported native primitive element type {elementType}.");
    }

    private static object BuildImmutableThenContainer<T>(T* data, ulong offset, int count, ContainerKind kind)
        where T : unmanaged
    {
        var builder = ImmutableArray.CreateBuilder<T>(count);
        for (int i = 0; i < count; i++)
            builder.Add(data[offset + (ulong)i]);
        return ContainerFactory.BuildTyped(kind, builder);
    }

    private static bool IsValid(ulong* validity, ulong row)
        => validity == null || (validity[row >> 6] & (1UL << (int)(row & 63))) != 0;

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
