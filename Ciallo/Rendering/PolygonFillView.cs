using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Ciallo.Geometry;
using Godot;
using Godot.Collections;

namespace Ciallo.Rendering;

/// <summary>A triangle fill with analytic coverage and bounded screen-space expansion.</summary>
public partial class PolygonFillView : MeshInstance2D
{
    private readonly ArrayMesh _mesh = new();
    private Rect2 _bounds;
    private Transform2D _boundsTransform;
    private int _boundsRevision = -1;
    private int _vertexCount;
    private Color _color = Colors.White;

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            SetInstanceShaderParameter("FillColor", value);
        }
    }

    public bool MissingBrush
    {
        get => Material == PolygonFillRendering.HintMaterial;
        set => Material = value ? PolygonFillRendering.HintMaterial : PolygonFillRendering.SolidMaterial;
    }

    public PolygonFillView()
    {
        Mesh = _mesh;
        MissingBrush = false;
    }

    public void SetColor(Color color) => Color = color;

    public void SetPolygonFromRawRing(ImmutableArray<Vector2> polygon) =>
        SetPolygonFromRawRing(polygon.AsSpan());

    public void SetPolygonFromRawRing(Vector2[] polygon) => SetPolygonFromRawRing(polygon.AsSpan());

    public void SetPolygonFromRawRing(ReadOnlySpan<Vector2> polygon)
    {
        if (polygon.IsEmpty) Clear();
        else SetPolygonFromRawRings([polygon.ToArray()]);
    }

    public void SetPolygonFromRawRings(Array<Vector2[]> polygons) =>
        SetTriangleResult(Arrangement2D.RepairAndTriangulate(polygons));

    public void SetTriangleResult(Dictionary result) =>
        SetTriangles((Vector2[])result["vertices"], (int[])result["indices"]);

    public void SetPolygonWithQueryResult(Arrangement arrangement, Vector2 point)
    {
        var face = arrangement.PointQueryFace(point);
        if (!face.IsValid || arrangement.IsUnboundedFace(face)) Clear();
        else SetTriangleResult(arrangement.GetTrianglesFromFace(face));
    }

    public void SetTriangles(ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> indices)
    {
        if (indices.IsEmpty)
        {
            Clear();
            return;
        }
        if (indices.Length % 3 != 0) throw new ArgumentException("Expected triangle index triplets.", nameof(indices));

        var positions = new Vector2[indices.Length];
        var custom0 = new float[indices.Length * 4];
        var custom1 = new float[indices.Length * 2];
        _bounds = new Rect2(vertices[indices[0]], Vector2.Zero);
        for (int i = 0; i < indices.Length; i += 3)
        {
            var a = vertices[indices[i]];
            var b = vertices[indices[i + 1]];
            var c = vertices[indices[i + 2]];
            positions[i] = a;
            positions[i + 1] = b;
            positions[i + 2] = c;
            _bounds = _bounds.Expand(a).Expand(b).Expand(c);
            for (int corner = 0; corner < 3; corner++)
            {
                int v = i + corner;
                custom0[v * 4] = a.X;
                custom0[v * 4 + 1] = a.Y;
                custom0[v * 4 + 2] = b.X;
                custom0[v * 4 + 3] = b.Y;
                custom1[v * 2] = c.X;
                custom1[v * 2 + 1] = c.Y;
            }
        }

        if (_vertexCount == indices.Length)
        {
            // The only attributes are two full-float custom channels, interleaved by Godot.
            var attributes = new float[indices.Length * 6];
            for (int i = 0; i < indices.Length; i++)
            {
                custom0.AsSpan(i * 4, 4).CopyTo(attributes.AsSpan(i * 6));
                custom1.AsSpan(i * 2, 2).CopyTo(attributes.AsSpan(i * 6 + 4));
            }
            _mesh.SurfaceUpdateVertexRegion(0, 0, MemoryMarshal.AsBytes(positions.AsSpan()));
            _mesh.SurfaceUpdateAttributeRegion(0, 0, MemoryMarshal.AsBytes(attributes.AsSpan()));
        }
        else
        {
            _mesh.ClearSurfaces();
            using Godot.Collections.Array arrays = [];
            arrays.Resize((int)Godot.Mesh.ArrayType.Max);
            arrays[(int)Godot.Mesh.ArrayType.Vertex] = positions;
            arrays[(int)Godot.Mesh.ArrayType.Custom0] = custom0;
            arrays[(int)Godot.Mesh.ArrayType.Custom1] = custom1;
            var format = Godot.Mesh.ArrayFormat.FlagUse2DVertices | Godot.Mesh.ArrayFormat.FlagUseDynamicUpdate |
                (Godot.Mesh.ArrayFormat)((ulong)Godot.Mesh.ArrayCustomFormat.RgbaFloat << (int)Godot.Mesh.ArrayFormat.FormatCustom0Shift) |
                (Godot.Mesh.ArrayFormat)((ulong)Godot.Mesh.ArrayCustomFormat.RgFloat << (int)Godot.Mesh.ArrayFormat.FormatCustom1Shift);
            _mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays, flags: format);
            _vertexCount = indices.Length;
        }
        _boundsRevision = -1;
        UpdateBounds();
    }

    public void Clear()
    {
        _mesh.ClearSurfaces();
        _vertexCount = 0;
        _bounds = default;
        _boundsRevision = -1;
        RenderingServer.CanvasItemSetCustomRect(GetCanvasItem(), false);
    }

    public override void _EnterTree()
    {
        RenderingServer.FramePreDraw += UpdateBounds;
        _boundsRevision = -1;
        UpdateBounds();
    }

    public override void _ExitTree() => RenderingServer.FramePreDraw -= UpdateBounds;

    private void UpdateBounds()
    {
        if (_vertexCount == 0 || !IsInsideTree()) return;
        var transform = GetGlobalTransformWithCanvas();
        if (_boundsRevision == PolygonFillRendering.BoundsRevision && transform == _boundsTransform) return;
        _boundsRevision = PolygonFillRendering.BoundsRevision;
        _boundsTransform = transform;
        var margin = Vector2.Zero;
        if (PolygonFillRendering.AaEnabled && transform.Determinant() != 0f)
        {
            var inverse = transform.AffineInverse();
            margin = new Vector2(new Vector2(inverse.X.X, inverse.Y.X).Length(),
                new Vector2(inverse.X.Y, inverse.Y.Y).Length()) * PolygonFillRendering.AaMaxVertexOffsetPixels;
        }
        var bounds = new Rect2(_bounds.Position - margin, _bounds.Size + 2f * margin);
        _mesh.CustomAabb = new Aabb(bounds.Position.X, bounds.Position.Y, 0f, bounds.Size.X, bounds.Size.Y, 0f);
        RenderingServer.CanvasItemSetCustomRect(GetCanvasItem(), true, bounds);
    }
}
