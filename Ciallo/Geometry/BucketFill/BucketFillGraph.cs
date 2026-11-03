using System;
using System.Collections.Generic;

namespace Ciallo.Geometry;

internal sealed class BucketFillGraph
{
    internal readonly int[] TriangleNodes;
    internal readonly double[] Capacities;
    internal readonly bool[] FrameNodes;
    private readonly int[] _offsets;
    private readonly (int Target, double Width)[] _connections;
    internal int Count => Capacities.Length;

    internal static BucketFillGraph BuildTriangles(CdtMesh2D mesh)
    {
        var nodes = new int[mesh.TriangleCount];
        for (int i = 0; i < nodes.Length; i++) nodes[i] = i;
        return new BucketFillGraph(mesh, nodes, nodes.Length);
    }

    internal static BucketFillGraph BuildGapRegions(CdtMesh2D mesh, HashSet<(double X, double Y)> endpoints)
    {
        var endpointVertices = new bool[mesh.Coordinates.Length / 2];
        for (int i = 0; i < endpointVertices.Length; i++)
            endpointVertices[i] = endpoints.Contains((mesh.Coordinates[2 * i], mesh.Coordinates[2 * i + 1]));

        // Gap candidates use existing CDT edges incident to original stroke endpoints.
        // Stroke points must be dense enough to offer suitable landing vertices on the opposite
        // stroke; users can subdivide sparse strokes. No projected landing points are inserted.
        var nodes = new int[mesh.TriangleCount];
        Array.Fill(nodes, -1);
        var queue = new Queue<int>();
        int count = 0;
        for (int first = 0; first < nodes.Length; first++)
        {
            if (nodes[first] >= 0) continue;
            nodes[first] = count;
            queue.Enqueue(first);
            while (queue.TryDequeue(out int triangle))
            {
                for (int h = triangle * 3; h < triangle * 3 + 3; h++)
                {
                    int opposite = mesh.Opposites[h];
                    if (opposite < 0 || mesh.Constrained[h] != 0) continue;
                    int next = opposite / 3;
                    if (nodes[next] >= 0 || endpointVertices[mesh.Triangles[h]] ||
                        endpointVertices[mesh.Triangles[CdtMesh2D.Next(h)]]) continue;
                    nodes[next] = count;
                    queue.Enqueue(next);
                }
            }
            count++;
        }
        return new BucketFillGraph(mesh, nodes, count);
    }

    private BucketFillGraph(CdtMesh2D mesh, int[] triangleNodes, int count)
    {
        TriangleNodes = triangleNodes;
        Capacities = new double[count];
        FrameNodes = new bool[count];
        _offsets = new int[count + 1];
        var widths = new double[mesh.Triangles.Length];
        for (int h = 0; h < mesh.Triangles.Length; h++)
        {
            int node = triangleNodes[h / 3];
            int a = mesh.Triangles[h], b = mesh.Triangles[CdtMesh2D.Next(h)];
            FrameNodes[node] |= mesh.FrameVertices[a] != 0;
            int opposite = mesh.Opposites[h];
            if (opposite < 0 || mesh.Constrained[h] != 0) continue;
            double dx = mesh.Coordinates[2 * a] - mesh.Coordinates[2 * b];
            double dy = mesh.Coordinates[2 * a + 1] - mesh.Coordinates[2 * b + 1];
            double width = widths[h] = Math.Sqrt(dx * dx + dy * dy);
            // Keep the region's interior scale even after its internal edges are contracted.
            Capacities[node] = Math.Max(Capacities[node], width);
            if (node != triangleNodes[opposite / 3]) _offsets[node + 1]++;
        }
        for (int i = 1; i < _offsets.Length; i++) _offsets[i] += _offsets[i - 1];
        _connections = new (int, double)[_offsets[^1]];
        var cursors = (int[])_offsets.Clone();
        for (int h = 0; h < mesh.Triangles.Length; h++)
        {
            int opposite = mesh.Opposites[h];
            if (opposite < 0 || mesh.Constrained[h] != 0) continue;
            int node = triangleNodes[h / 3], next = triangleNodes[opposite / 3];
            if (node != next) _connections[cursors[node]++] = (next, widths[h]);
        }
    }

    internal void Propagate(int seed, int label, int[] labels, double[] weights)
    {
        labels[seed] = label;
        weights[seed] = Capacities[seed];
        var queue = new Queue<int>();
        queue.Enqueue(seed);
        while (queue.TryDequeue(out int node))
        {
            for (int i = _offsets[node]; i < _offsets[node + 1]; i++)
            {
                var (next, width) = _connections[i];
                double weight = Math.Min(width, weights[node]);
                if (weight > weights[next] || (weight == weights[next] && labels[next] != label))
                {
                    weights[next] = weight;
                    labels[next] = label;
                    queue.Enqueue(next);
                }
            }
        }
    }
}
