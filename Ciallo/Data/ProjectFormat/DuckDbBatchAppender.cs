using System;
using System.Collections;
using System.Collections.Generic;
using DuckDB.NET.Data;
using DuckDB.NET.Native;
using NativeDuckDbAppender = DuckDB.NET.Native.DuckDBAppender;

namespace Ciallo.Data;

/// <summary>
/// Opens the project-local batch appender over DuckDB.NET's public native connection.
/// </summary>
internal static class DuckDbBatchAppenderExtensions
{
    public static DuckDbBatchAppender CreateBatchAppender(
        this DuckDBConnection connection,
        string table) => new(connection.NativeConnection, table);
}

/// <summary>
/// Row-oriented facade over DuckDB's data-chunk appender. Composite values are written recursively
/// into child vectors, while callers remain isolated from native handles and vector layout.
/// </summary>
internal sealed class DuckDbBatchAppender : IDisposable
{
    private readonly NativeDuckDbAppender _appender;
    private readonly DuckDBLogicalType[] _logicalTypes;
    private readonly int[][][] _structLeafPaths;
    private readonly DuckDBDataChunk _chunk;
    private readonly DuckDbBatchAppenderRow _row;
    private readonly ulong _vectorSize = NativeMethods.Helpers.DuckDBVectorSize();
    private ulong _rowCount;

    public DuckDbBatchAppender(DuckDBNativeConnection connection, string table)
    {
        var state = NativeMethods.Appender.DuckDBAppenderCreate(connection, null, table, out var appender);
        if (!state.IsSuccess())
        {
            try
            {
                throw new InvalidOperationException(
                    $"Could not create DuckDB appender: {NativeMethods.Appender.DuckDBAppenderError(appender)}");
            }
            finally
            {
                appender.Dispose();
            }
        }
        _appender = appender;

        var columnCount = NativeMethods.Appender.DuckDBAppenderColumnCount(_appender);
        _logicalTypes = new DuckDBLogicalType[columnCount];
        _structLeafPaths = new int[columnCount][][];
        var handles = new IntPtr[columnCount];
        for (ulong i = 0; i < columnCount; i++)
        {
            _logicalTypes[i] = NativeMethods.Appender.DuckDBAppenderColumnType(_appender, i);
            _structLeafPaths[i] = DuckDbVectorWriter.BuildStructLeafPaths(_logicalTypes[i]);
            handles[i] = _logicalTypes[i].DangerousGetHandle();
        }

        _chunk = NativeMethods.DataChunks.DuckDBCreateDataChunk(handles, columnCount);
        _row = new DuckDbBatchAppenderRow(this);
    }

    public DuckDbBatchAppenderRow CreateRow()
    {
        if (_rowCount == _vectorSize)
            FlushChunk();
        _row.Reset(_rowCount);
        return _row;
    }

    internal IntPtr GetVector(int column) =>
        NativeMethods.DataChunks.DuckDBDataChunkGetVector(_chunk, column);

    internal int[][] GetStructLeafPaths(int column) => _structLeafPaths[column];

    internal void CompleteRow() => _rowCount++;

    internal void CheckListState(DuckDBState state, string operation)
    {
        if (!state.IsSuccess())
            throw new InvalidOperationException(operation + " failed.");
    }

    private void FlushChunk()
    {
        if (_rowCount == 0)
            return;

        NativeMethods.DataChunks.DuckDBDataChunkSetSize(_chunk, _rowCount);
        CheckAppender(
            NativeMethods.Appender.DuckDBAppendDataChunk(_appender, _chunk),
            "Could not append DuckDB data chunk");
        NativeMethods.DataChunks.DuckDBDataChunkReset(_chunk);
        _rowCount = 0;
    }

    public void Dispose()
    {
        try
        {
            FlushChunk();
            CheckAppender(
                NativeMethods.Appender.DuckDBAppenderClose(_appender),
                "Could not close DuckDB appender");
        }
        finally
        {
            try
            {
                _chunk.Dispose();
                foreach (var logicalType in _logicalTypes)
                    logicalType.Dispose();
            }
            finally
            {
                _appender.Dispose();
            }
        }
    }

    private void CheckAppender(DuckDBState state, string operation)
    {
        if (state.IsSuccess())
            return;

        throw new InvalidOperationException(
            $"{operation}: {NativeMethods.Appender.DuckDBAppenderError(_appender)}");
    }
}

/// <summary>
/// One sequential appender row. Its surface deliberately follows DuckDB.NET's managed row writer;
/// composite methods are project-local extensions.
/// </summary>
internal sealed class DuckDbBatchAppenderRow
{
    private readonly DuckDbBatchAppender _owner;
    private ulong _rowIndex;
    private int _columnIndex;
    // Reused across rows: leaf FLOAT* pointers for the current STRUCT[] column.
    private nint[] _leafDataScratch = [];

    internal DuckDbBatchAppenderRow(DuckDbBatchAppender owner)
    {
        _owner = owner;
    }

    internal void Reset(ulong rowIndex)
    {
        _rowIndex = rowIndex;
        _columnIndex = 0;
    }

    public DuckDbBatchAppenderRow AppendValue(object value)
    {
        var vector = NextVector();
        if (value == null)
            DuckDbVectorWriter.WriteNull(vector, _rowIndex);
        else
            DuckDbVectorWriter.WriteScalar(vector, _rowIndex, value);
        return this;
    }

    public DuckDbBatchAppenderRow AppendValue(byte[] value)
    {
        var vector = NextVector();
        if (value == null)
            DuckDbVectorWriter.WriteNull(vector, _rowIndex);
        else
            DuckDbVectorWriter.WriteBlob(vector, _rowIndex, value);
        return this;
    }

    public DuckDbBatchAppenderRow AppendStruct(float[] leaves)
    {
        var column = _columnIndex++;
        var vector = _owner.GetVector(column);
        if (leaves == null)
        {
            DuckDbVectorWriter.WriteNull(vector, _rowIndex);
            return this;
        }

        var leafPaths = _owner.GetStructLeafPaths(column);
        for (int i = 0; i < leaves.Length; i++)
            DuckDbVectorWriter.WriteScalar(
                DuckDbVectorWriter.GetStructLeafVector(vector, leafPaths[i]),
                _rowIndex,
                leaves[i]);
        return this;
    }

    public unsafe DuckDbBatchAppenderRow AppendValue(IList values)
    {
        var vector = NextVector();
        if (values == null)
        {
            WriteEmptyList(vector);
            return this;
        }

        var offset = (ulong)NativeMethods.Vectors.DuckDBListVectorGetSize(vector);
        var required = offset + (ulong)values.Count;
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorReserve(vector, required),
            "Reserving DuckDB list vector");

        var child = NativeMethods.Vectors.DuckDBListVectorGetChild(vector);
        for (int i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value == null)
                DuckDbVectorWriter.WriteNull(child, offset + (ulong)i);
            else
                DuckDbVectorWriter.WriteScalar(child, offset + (ulong)i, value);
        }

        var entries = (DuckDBListEntry*)NativeMethods.Vectors.DuckDBVectorGetData(vector);
        entries[_rowIndex] = new DuckDBListEntry(offset, (ulong)values.Count);
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorSetSize(vector, required),
            "Sizing DuckDB list vector");
        return this;
    }

    public unsafe DuckDbBatchAppenderRow AppendStructList(List<float>[] leafColumns)
    {
        var column = _columnIndex++;
        var vector = _owner.GetVector(column);
        if (leafColumns == null)
        {
            WriteEmptyList(vector);
            return this;
        }

        var count = leafColumns[0].Count;
        var offset = (ulong)NativeMethods.Vectors.DuckDBListVectorGetSize(vector);
        var required = offset + (ulong)count;
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorReserve(vector, required),
            "Reserving DuckDB struct-list vector");

        var structVector = NativeMethods.Vectors.DuckDBListVectorGetChild(vector);
        var leafPaths = _owner.GetStructLeafPaths(column);
        for (int leaf = 0; leaf < leafColumns.Length; leaf++)
        {
            var child = DuckDbVectorWriter.GetStructLeafVector(structVector, leafPaths[leaf]);
            for (int i = 0; i < count; i++)
                DuckDbVectorWriter.WriteScalar(child, offset + (ulong)i, leafColumns[leaf][i]);
        }

        var entries = (DuckDBListEntry*)NativeMethods.Vectors.DuckDBVectorGetData(vector);
        entries[_rowIndex] = new DuckDBListEntry(offset, (ulong)count);
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorSetSize(vector, required),
            "Sizing DuckDB struct-list vector");
        return this;
    }

    /// <summary>
    /// Write a STRUCT[] column straight from the raw field value via its codec, with no
    /// intermediate <c>List&lt;float&gt;[]</c> and no per-element boxing. The hot path for
    /// stroke geometry (Positions/Tilts).
    /// </summary>
    public unsafe DuckDbBatchAppenderRow AppendStructList(StructCodec codec, object arrayValue)
    {
        var column = _columnIndex++;
        var vector = _owner.GetVector(column);
        if (arrayValue == null)
        {
            WriteEmptyList(vector);
            return this;
        }

        var count = codec.GetArrayLength(arrayValue);
        var offset = (ulong)NativeMethods.Vectors.DuckDBListVectorGetSize(vector);
        var required = offset + (ulong)count;
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorReserve(vector, required),
            "Reserving DuckDB struct-list vector");

        if (count > 0)
        {
            var structVector = NativeMethods.Vectors.DuckDBListVectorGetChild(vector);
            var leafPaths = _owner.GetStructLeafPaths(column);
            var leafData = _leafDataScratch.Length >= leafPaths.Length
                ? _leafDataScratch.AsSpan(0, leafPaths.Length)
                : (_leafDataScratch = new nint[leafPaths.Length]).AsSpan();
            for (int leaf = 0; leaf < leafPaths.Length; leaf++)
                leafData[leaf] = (nint)NativeMethods.Vectors.DuckDBVectorGetData(
                    DuckDbVectorWriter.GetStructLeafVector(structVector, leafPaths[leaf]));
            codec.DecomposeArrayInto(arrayValue, leafData, offset);
        }

        var entries = (DuckDBListEntry*)NativeMethods.Vectors.DuckDBVectorGetData(vector);
        entries[_rowIndex] = new DuckDBListEntry(offset, (ulong)count);
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorSetSize(vector, required),
            "Sizing DuckDB struct-list vector");
        return this;
    }

    /// <summary>
    /// Write a primitive scalar list column (e.g. FLOAT[]/INTEGER[]) straight from a contiguous
    /// span, copying the block into the list child vector with no boxing. The hot path for
    /// stroke Radii/Pressures.
    /// </summary>
    public unsafe DuckDbBatchAppenderRow AppendPrimitiveList<T>(ReadOnlySpan<T> values)
        where T : unmanaged
    {
        var vector = NextVector();
        var count = values.Length;
        var offset = (ulong)NativeMethods.Vectors.DuckDBListVectorGetSize(vector);
        var required = offset + (ulong)count;
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorReserve(vector, required),
            "Reserving DuckDB primitive-list vector");

        if (count > 0)
        {
            var child = NativeMethods.Vectors.DuckDBListVectorGetChild(vector);
            var dst = (T*)NativeMethods.Vectors.DuckDBVectorGetData(child) + offset;
            values.CopyTo(new Span<T>(dst, count));
        }

        var entries = (DuckDBListEntry*)NativeMethods.Vectors.DuckDBVectorGetData(vector);
        entries[_rowIndex] = new DuckDBListEntry(offset, (ulong)count);
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorSetSize(vector, required),
            "Sizing DuckDB primitive-list vector");
        return this;
    }

    // A list/map row that carries no elements still needs a valid (offset, 0) entry: DuckDB's
    // reused data chunk is not zeroed by DuckDBDataChunkReset, so a leftover entry from a prior
    // chunk would otherwise describe out-of-range child rows.
    private unsafe void WriteEmptyList(IntPtr vector)
    {
        var offset = (ulong)NativeMethods.Vectors.DuckDBListVectorGetSize(vector);
        var entries = (DuckDBListEntry*)NativeMethods.Vectors.DuckDBVectorGetData(vector);
        entries[_rowIndex] = new DuckDBListEntry(offset, 0);
        DuckDbVectorWriter.WriteNull(vector, _rowIndex);
    }

    public unsafe DuckDbBatchAppenderRow AppendMap(IList keys, IList values)
    {
        var vector = NextVector();
        if (keys == null)
        {
            WriteEmptyList(vector);
            return this;
        }

        var offset = (ulong)NativeMethods.Vectors.DuckDBListVectorGetSize(vector);
        var required = offset + (ulong)keys.Count;
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorReserve(vector, required),
            "Reserving DuckDB map vector");

        var entryVector = NativeMethods.Vectors.DuckDBListVectorGetChild(vector);
        var keyVector = NativeMethods.Vectors.DuckDBStructVectorGetChild(entryVector, 0);
        var valueVector = NativeMethods.Vectors.DuckDBStructVectorGetChild(entryVector, 1);
        for (int i = 0; i < keys.Count; i++)
        {
            DuckDbVectorWriter.WriteScalar(keyVector, offset + (ulong)i, keys[i]);
            DuckDbVectorWriter.WriteScalar(valueVector, offset + (ulong)i, values[i]);
        }

        var entries = (DuckDBListEntry*)NativeMethods.Vectors.DuckDBVectorGetData(vector);
        entries[_rowIndex] = new DuckDBListEntry(offset, (ulong)keys.Count);
        _owner.CheckListState(
            NativeMethods.Vectors.DuckDBListVectorSetSize(vector, required),
            "Sizing DuckDB map vector");
        return this;
    }

    public void EndRow() => _owner.CompleteRow();

    private IntPtr NextVector() => _owner.GetVector(_columnIndex++);
}

internal static unsafe class DuckDbVectorWriter
{
    private const int GuidSize = 16;
    private static readonly int[] GuidByteOrder = [6, 7, 4, 5, 0, 1, 2, 3, 15, 14, 13, 12, 11, 10, 9, 8];

    public static int[][] BuildStructLeafPaths(DuckDBLogicalType logicalType)
    {
        if (NativeMethods.LogicalType.DuckDBGetTypeId(logicalType) == DuckDBType.List)
        {
            using var childType = NativeMethods.LogicalType.DuckDBListTypeChildType(logicalType);
            if (NativeMethods.LogicalType.DuckDBGetTypeId(childType) != DuckDBType.Struct)
                return [];
            return BuildStructLeafPathsCore(childType);
        }

        if (NativeMethods.LogicalType.DuckDBGetTypeId(logicalType) != DuckDBType.Struct)
            return [];
        return BuildStructLeafPathsCore(logicalType);
    }

    public static IntPtr GetStructLeafVector(IntPtr vector, int[] path)
    {
        foreach (var childIndex in path)
            vector = NativeMethods.Vectors.DuckDBStructVectorGetChild(vector, childIndex);
        return vector;
    }

    private static int[][] BuildStructLeafPathsCore(DuckDBLogicalType logicalType)
    {
        var paths = new List<int[]>();
        CollectStructLeafPaths(logicalType, [], paths);
        return paths.ToArray();
    }

    private static void CollectStructLeafPaths(
        DuckDBLogicalType logicalType,
        List<int> path,
        List<int[]> paths)
    {
        var childCount = NativeMethods.LogicalType.DuckDBStructTypeChildCount(logicalType);
        for (long i = 0; i < childCount; i++)
        {
            using var childType = NativeMethods.LogicalType.DuckDBStructTypeChildType(logicalType, i);
            path.Add((int)i);
            if (NativeMethods.LogicalType.DuckDBGetTypeId(childType) == DuckDBType.Struct)
                CollectStructLeafPaths(childType, path, paths);
            else
                paths.Add(path.ToArray());
            path.RemoveAt(path.Count - 1);
        }
    }

    public static void WriteScalar(IntPtr vector, ulong index, object value)
    {
        var data = NativeMethods.Vectors.DuckDBVectorGetData(vector);
        switch (value)
        {
            case bool v: ((bool*)data)[index] = v; return;
            case sbyte v: ((sbyte*)data)[index] = v; return;
            case short v: ((short*)data)[index] = v; return;
            case int v: ((int*)data)[index] = v; return;
            case long v: ((long*)data)[index] = v; return;
            case byte v: ((byte*)data)[index] = v; return;
            case ushort v: ((ushort*)data)[index] = v; return;
            case uint v: ((uint*)data)[index] = v; return;
            case ulong v: ((ulong*)data)[index] = v; return;
            case float v: ((float*)data)[index] = v; return;
            case double v: ((double*)data)[index] = v; return;
            case Guid v: ((DuckDBHugeInt*)data)[index] = ToHugeInt(v); return;
            case string v:
                NativeMethods.Vectors.DuckDBVectorAssignStringElement(vector, index, v);
                return;
            case Enum v:
                ((int*)data)[index] = Convert.ToInt32(v);
                return;
            default:
                throw new InvalidOperationException($"Cannot append CLR value {value.GetType()} to DuckDB.");
        }
    }

    public static void WriteNull(IntPtr vector, ulong index)
    {
        NativeMethods.Vectors.DuckDBVectorEnsureValidityWritable(vector);
        var validity = NativeMethods.Vectors.DuckDBVectorGetValidity(vector);
        NativeMethods.ValidityMask.DuckDBValiditySetRowValidity(validity, index, false);
    }

    public static void WriteBlob(IntPtr vector, ulong index, byte[] value)
    {
        fixed (byte* bytes = value)
            NativeMethods.Vectors.DuckDBVectorAssignStringElementLength(vector, index, bytes, value.Length);
    }

    private static DuckDBHugeInt ToHugeInt(Guid guid)
    {
        Span<byte> bytes = stackalloc byte[32];
        guid.TryWriteBytes(bytes);
        for (int i = 0; i < GuidSize; i++)
            bytes[i + GuidSize] = bytes[GuidByteOrder[i]];

        var upper = BitConverter.ToInt64(bytes[GuidSize..]);
        var lower = BitConverter.ToUInt64(bytes[(GuidSize + 8)..]);
        upper ^= (long)1 << 63;
        return new DuckDBHugeInt(lower, upper);
    }
}
