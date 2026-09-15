using System;
using Godot;

namespace Ciallo.Geometry;

// Owns one native snapshot. The fill context uses it synchronously on the main thread.
public sealed class CdtMesh2D : IDisposable
{
    private readonly ConstrainedTriangulation2D _native;
    internal readonly double[] Coordinates;
    internal readonly int[] Triangles;
    internal readonly int[] Opposites;
    internal readonly byte[] Constrained;
    internal readonly byte[] FrameVertices;
    public int TriangleCount => Triangles.Length / 3;

    public CdtMesh2D(Vector2[] vertices, int[] constraints, int[] frameVertices)
    {
        _native = ConstrainedTriangulation2D.Build(vertices, constraints, frameVertices);
        using var graph = _native.GetGraph();
        Coordinates = (double[])graph["vertices"];
        Triangles = (int[])graph["triangles"];
        Opposites = (int[])graph["opposites"];
        Constrained = (byte[])graph["constrained"];
        FrameVertices = (byte[])graph["frame_vertices"];
    }

    public int Locate(Vector2 point) => _native.Locate(point);
    internal Vector2 Position(int vertex) => new((float)Coordinates[2 * vertex], (float)Coordinates[2 * vertex + 1]);
    internal static int Next(int halfedge) => halfedge / 3 * 3 + (halfedge + 1) % 3;
    public void Dispose() => _native.Dispose();
}
