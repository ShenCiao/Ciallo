using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Ciallo.Geometry;
using Godot;

namespace Ciallo.Data;

/// <summary>
/// Maps a creative value type (Color, Vector2, Transform2D, BezierPoint) to a DuckDB STRUCT.
///
/// A codec is the single source of truth for one structured type's project-format contract:
/// its DuckDB column type, how to decompose a CLR value into flat FLOAT leaves, and how to
/// recompose a value from the Dictionary DuckDB returns on read.
///
/// All leaves are FLOAT. Their order follows a depth-first traversal of the DuckDB STRUCT type,
/// which is the same order consumed by <see cref="DuckDbBatchAppender"/>.
/// </summary>
internal abstract class StructCodec
{
    public abstract Type TargetType { get; }
    public abstract int LeafCount { get; }

    /// <summary>Full DuckDB type, e.g. <c>STRUCT(r FLOAT, g FLOAT, b FLOAT, a FLOAT)</c>.</summary>
    public abstract string DuckDbType { get; }

    /// <summary>Flatten a single CLR value into <paramref name="leaves"/> (length == <see cref="LeafCount"/>).
    /// Boxes one value; used for the single-element <c>Struct</c> shape, which is not a bulk hot path.</summary>
    public abstract void Decompose(object value, float[] leaves);

    /// <summary>Rebuild a single CLR value from the Dictionary DuckDB returns for a STRUCT.</summary>
    public abstract object Compose(IReadOnlyDictionary<string, object> dict);

    /// <summary>
    /// Flatten every element of a StructArray field value into per-leaf FLOAT columns, without
    /// boxing each element. <paramref name="arrayValue"/> is the raw field value (e.g.
    /// ImmutableArray&lt;Vector2&gt;); <paramref name="leafColumns"/> has length == LeafCount.
    /// </summary>
    public abstract void DecomposeInto(object arrayValue, List<float>[] leafColumns);

    /// <summary>
    /// Rebuild a StructArray field value from the rows DuckDB returns for a STRUCT[] column,
    /// producing the field's declared container. Each row's leaf floats are read without boxing
    /// the composed element into object[].
    /// </summary>
    public abstract object ComposeArray(System.Collections.IEnumerable dbRows, ContainerKind containerKind);

    protected static float F(object o) => Convert.ToSingle(o);

    protected static Vector2 ReadVector2(object o)
    {
        var d = (IReadOnlyDictionary<string, object>)o;
        return new Vector2(F(d["x"]), F(d["y"]));
    }
}

/// <summary>
/// Strongly-typed codec base. <see cref="Decompose(in T, System.Span{float})"/> and
/// <see cref="Compose(System.ReadOnlySpan{float})"/> touch no boxes; the non-generic object overloads
/// forward to them so single-element Struct fields keep working.
/// </summary>
internal abstract class StructCodec<T> : StructCodec
{
    public sealed override Type TargetType => typeof(T);

    /// <summary>Flatten one strongly-typed value into <paramref name="leaves"/> — no boxing.</summary>
    public abstract void Decompose(in T value, Span<float> leaves);

    /// <summary>Rebuild one strongly-typed value from its leaf floats — no boxing.</summary>
    public abstract T Compose(ReadOnlySpan<float> leaves);

    public sealed override void Decompose(object value, float[] leaves) => Decompose((T)value, leaves);

    public sealed override object Compose(IReadOnlyDictionary<string, object> dict)
    {
        Span<float> leaves = stackalloc float[LeafCount];
        ReadLeaves(dict, leaves);
        return Compose(leaves);
    }

    /// <summary>Read the STRUCT's named leaves from the Dictionary DuckDB returns, in leaf order.</summary>
    protected abstract void ReadLeaves(IReadOnlyDictionary<string, object> dict, Span<float> leaves);

    public sealed override void DecomposeInto(object arrayValue, List<float>[] leafColumns)
    {
        Span<float> leaves = stackalloc float[LeafCount];
        foreach (var element in AsReadOnlyList(arrayValue))
        {
            Decompose(element, leaves);
            for (int i = 0; i < leafColumns.Length; i++)
                leafColumns[i].Add(leaves[i]);
        }
    }

    public sealed override object ComposeArray(System.Collections.IEnumerable dbRows, ContainerKind containerKind)
    {
        var builder = ImmutableArray.CreateBuilder<T>();
        Span<float> leaves = stackalloc float[LeafCount];
        foreach (var row in dbRows)
        {
            ReadLeaves(FieldDescriptor.AsStructDict(row), leaves);
            builder.Add(Compose(leaves));
        }
        return ContainerFactory.BuildTyped(containerKind, builder);
    }

    private static IReadOnlyList<T> AsReadOnlyList(object arrayValue)
    {
        return arrayValue switch
        {
            ImmutableArray<T> immutable => immutable.IsDefault ? [] : immutable,
            IReadOnlyList<T> list => list,
            _ => throw new InvalidOperationException(
                $"StructArray value {arrayValue?.GetType()} is not a supported container of {typeof(T)}."),
        };
    }
}

internal sealed class ColorCodec : StructCodec<Color>
{
    public override int LeafCount => 4;
    public override string DuckDbType => "STRUCT(r FLOAT, g FLOAT, b FLOAT, a FLOAT)";

    public override void Decompose(in Color value, Span<float> leaves)
    {
        leaves[0] = value.R;
        leaves[1] = value.G;
        leaves[2] = value.B;
        leaves[3] = value.A;
    }

    public override Color Compose(ReadOnlySpan<float> leaves) =>
        new(leaves[0], leaves[1], leaves[2], leaves[3]);

    protected override void ReadLeaves(IReadOnlyDictionary<string, object> dict, Span<float> leaves)
    {
        leaves[0] = F(dict["r"]);
        leaves[1] = F(dict["g"]);
        leaves[2] = F(dict["b"]);
        leaves[3] = F(dict["a"]);
    }
}

internal sealed class Vector2Codec : StructCodec<Vector2>
{
    public override int LeafCount => 2;
    public override string DuckDbType => "STRUCT(x FLOAT, y FLOAT)";

    public override void Decompose(in Vector2 value, Span<float> leaves)
    {
        leaves[0] = value.X;
        leaves[1] = value.Y;
    }

    public override Vector2 Compose(ReadOnlySpan<float> leaves) =>
        new(leaves[0], leaves[1]);

    protected override void ReadLeaves(IReadOnlyDictionary<string, object> dict, Span<float> leaves)
    {
        leaves[0] = F(dict["x"]);
        leaves[1] = F(dict["y"]);
    }
}

internal sealed class Transform2DCodec : StructCodec<Transform2D>
{
    public override int LeafCount => 6;

    public override string DuckDbType =>
        "STRUCT(x STRUCT(x FLOAT, y FLOAT), y STRUCT(x FLOAT, y FLOAT), origin STRUCT(x FLOAT, y FLOAT))";

    public override void Decompose(in Transform2D value, Span<float> leaves)
    {
        leaves[0] = value.X.X;
        leaves[1] = value.X.Y;
        leaves[2] = value.Y.X;
        leaves[3] = value.Y.Y;
        leaves[4] = value.Origin.X;
        leaves[5] = value.Origin.Y;
    }

    public override Transform2D Compose(ReadOnlySpan<float> leaves) =>
        new(new Vector2(leaves[0], leaves[1]), new Vector2(leaves[2], leaves[3]), new Vector2(leaves[4], leaves[5]));

    protected override void ReadLeaves(IReadOnlyDictionary<string, object> dict, Span<float> leaves)
    {
        var x = ReadVector2(dict["x"]);
        var y = ReadVector2(dict["y"]);
        var origin = ReadVector2(dict["origin"]);
        leaves[0] = x.X;
        leaves[1] = x.Y;
        leaves[2] = y.X;
        leaves[3] = y.Y;
        leaves[4] = origin.X;
        leaves[5] = origin.Y;
    }
}

internal sealed class BezierPointCodec : StructCodec<BezierPoint>
{
    public override int LeafCount => 6;

    // "in" and "out" are DuckDB reserved words, so they must be quoted in the type definition.
    public override string DuckDbType =>
        "STRUCT(p STRUCT(x FLOAT, y FLOAT), \"in\" STRUCT(x FLOAT, y FLOAT), \"out\" STRUCT(x FLOAT, y FLOAT))";

    public override void Decompose(in BezierPoint value, Span<float> leaves)
    {
        leaves[0] = value.P.X;
        leaves[1] = value.P.Y;
        leaves[2] = value.In.X;
        leaves[3] = value.In.Y;
        leaves[4] = value.Out.X;
        leaves[5] = value.Out.Y;
    }

    public override BezierPoint Compose(ReadOnlySpan<float> leaves) =>
        new(new Vector2(leaves[0], leaves[1]), new Vector2(leaves[2], leaves[3]), new Vector2(leaves[4], leaves[5]));

    protected override void ReadLeaves(IReadOnlyDictionary<string, object> dict, Span<float> leaves)
    {
        var p = ReadVector2(dict["p"]);
        var i = ReadVector2(dict["in"]);
        var o = ReadVector2(dict["out"]);
        leaves[0] = p.X;
        leaves[1] = p.Y;
        leaves[2] = i.X;
        leaves[3] = i.Y;
        leaves[4] = o.X;
        leaves[5] = o.Y;
    }
}

internal static class StructCodecRegistry
{
    private static readonly Dictionary<Type, StructCodec> Codecs = new()
    {
        [typeof(Color)] = new ColorCodec(),
        [typeof(Vector2)] = new Vector2Codec(),
        [typeof(Transform2D)] = new Transform2DCodec(),
        [typeof(BezierPoint)] = new BezierPointCodec(),
    };

    public static bool TryGet(Type type, out StructCodec codec) => Codecs.TryGetValue(type, out codec);

    public static StructCodec Get(Type type) => Codecs[type];
}
