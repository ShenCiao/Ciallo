using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Godot;

namespace Ciallo.Geometry;

public sealed class BucketFillSolver : IDisposable
{
    private readonly CdtMesh2D _mesh;
    private readonly double[] _edgeWidths;
    private readonly double[] _capacities;
    private readonly bool[] _frameTriangles;
    private int[] _outsideLabels;
    private double[] _outsideWeights;
    private int _cachedSeed = -1;
    private bool _cachedGapAware;
    private double _cachedGapFactor;
    private BucketFillRegion _cachedRegion;
    public int TriangleCount => _mesh.TriangleCount;

    public static BucketFillSolver Build(IReadOnlyList<ImmutableArray<Vector2>> strokes)
    {
        var vertices = new List<Vector2>();
        var constraints = new List<int>();
        foreach (var stroke in strokes)
        {
            if (stroke.Length < 2) continue;
            int first = vertices.Count;
            vertices.Add(stroke[0]);
            for (int i = 1; i < stroke.Length; i++)
            {
                if (stroke[i] == vertices[^1]) continue;
                constraints.Add(vertices.Count - 1);
                constraints.Add(vertices.Count);
                vertices.Add(stroke[i]);
            }
            if (vertices.Count == first + 1) vertices.RemoveAt(first);
        }
        int[] frame = [];
        if (vertices.Count > 0)
        {
            Vector2 min = vertices[0], max = vertices[0];
            foreach (var point in vertices)
            {
                min = min.Min(point);
                max = max.Max(point);
            }
            // GP pads the exterior so frame connections are wider than gaps inside the drawing.
            float padding = Math.Max(Math.Max(max.X - min.X, max.Y - min.Y) * 1.1f, 1f);
            min -= new Vector2(padding, padding);
            max += new Vector2(padding, padding);
            int start = vertices.Count;
            vertices.AddRange([min, new(max.X, min.Y), max, new(min.X, max.Y)]);
            frame = [start, start + 1, start + 2, start + 3];
        }
        return new BucketFillSolver(new CdtMesh2D([.. vertices], [.. constraints], frame));
    }

    private BucketFillSolver(CdtMesh2D mesh)
    {
        _mesh = mesh;
        _edgeWidths = new double[mesh.Triangles.Length];
        _capacities = new double[mesh.TriangleCount];
        _frameTriangles = new bool[mesh.TriangleCount];
        for (int h = 0; h < mesh.Triangles.Length; h++)
        {
            int a = mesh.Triangles[h], b = mesh.Triangles[CdtMesh2D.Next(h)];
            double dx = mesh.Coordinates[2 * a] - mesh.Coordinates[2 * b];
            double dy = mesh.Coordinates[2 * a + 1] - mesh.Coordinates[2 * b + 1];
            double width = _edgeWidths[h] = Math.Sqrt(dx * dx + dy * dy);
            if (mesh.Opposites[h] >= 0 && mesh.Constrained[h] == 0)
                _capacities[h / 3] = Math.Max(_capacities[h / 3], width);
            _frameTriangles[h / 3] |= mesh.FrameVertices[a] != 0;
        }
    }

    public BucketFillRegion Query(Vector2 seed, bool gapAware, double gapFactor)
    {
        if (!double.IsFinite(gapFactor) || gapFactor is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(gapFactor), "Gap factor must be between zero and one.");
        int start = _mesh.Locate(seed);
        if (start < 0 || _frameTriangles[start]) return BucketFillRegion.Empty;
        if (_cachedSeed == start && _cachedGapAware == gapAware && _cachedGapFactor == gapFactor) return _cachedRegion;
        int[] labels;
        double[] weights;
        int label = 1;
        if (gapAware && gapFactor > 0)
        {
            labels = new int[TriangleCount];
            Array.Fill(labels, -1);
            weights = new double[TriangleCount];
            Propagate(start, 0, labels, weights);
            // GP seeds regions that are not reached at their local full capacity.
            while (true)
            {
                int next = -1;
                double best = 0;
                for (int i = 0; i < TriangleCount; i++)
                    if (weights[i] < _capacities[i] * gapFactor && weights[i] > best)
                    {
                        next = i;
                        best = weights[i];
                    }
                if (next < 0) break;
                Propagate(next, label++, labels, weights);
            }
        }
        else
        {
            // Exterior competition is only used with gap detection disabled (or a zero factor).
            if (_outsideWeights == null)
            {
                _outsideLabels = new int[TriangleCount];
                Array.Fill(_outsideLabels, -1);
                _outsideWeights = new double[TriangleCount];
                int exterior = Array.FindIndex(_frameTriangles, value => value);
                Propagate(exterior, 0, _outsideLabels, _outsideWeights);
            }
            labels = (int[])_outsideLabels.Clone();
            weights = (double[])_outsideWeights.Clone();
        }
        // The click wins equal-width competition in GP's final propagation pass.
        Propagate(start, label, labels, weights);
        var selected = new bool[TriangleCount];
        bool touchesFrame = false;
        for (int i = 0; i < TriangleCount; i++)
        {
            selected[i] = labels[i] == label;
            if (selected[i] && _frameTriangles[i])
            {
                touchesFrame = true;
                break;
            }
        }
        // Rejected regions must also reach the cache, or hovering repeats the full propagation.
        var region = touchesFrame ? BucketFillRegion.Empty : ExtractRegion(selected);
        _cachedSeed = start;
        _cachedGapAware = gapAware;
        _cachedGapFactor = gapFactor;
        return _cachedRegion = region;
    }

    private void Propagate(int seed, int label, int[] labels, double[] weights)
    {
        labels[seed] = label;
        weights[seed] = _capacities[seed];
        var queue = new Queue<int>();
        queue.Enqueue(seed);
        while (queue.TryDequeue(out int triangle))
        {
            for (int h = triangle * 3; h < triangle * 3 + 3; h++)
            {
                int opposite = _mesh.Opposites[h];
                if (opposite < 0 || _mesh.Constrained[h] != 0) continue;
                int next = opposite / 3;
                double weight = Math.Min(_edgeWidths[h], weights[triangle]);
                if (weight > weights[next] || (weight == weights[next] && labels[next] != label))
                {
                    weights[next] = weight;
                    labels[next] = label;
                    queue.Enqueue(next);
                }
            }
        }
    }

    private BucketFillRegion ExtractRegion(bool[] selected)
    {
        var visited = new bool[_mesh.Triangles.Length];
        var outers = new List<(Vector2[] Points, double Area)>();
        var holes = new List<Vector2[]>();
        for (int first = 0; first < visited.Length; first++)
        {
            if (visited[first] || !IsBoundary(first)) continue;
            var ring = new List<Vector2>();
            int h = first;
            do
            {
                if (visited[h]) throw new InvalidOperationException("CDT boundary traversal revisited another contour.");
                visited[h] = true;
                var point = _mesh.Position(_mesh.Triangles[h]);
                if (ring.Count == 0 || ring[^1] != point) ring.Add(point);
                h = CdtMesh2D.Next(h);
                // Follow the selected fan at this vertex, including when contours touch.
                while (_mesh.Opposites[h] >= 0 && selected[_mesh.Opposites[h] / 3])
                    h = CdtMesh2D.Next(_mesh.Opposites[h]);
            } while (h != first);
            if (ring.Count > 1 && ring[0] == ring[^1]) ring.RemoveAt(ring.Count - 1);
            if (ring.Count < 3) continue;
            double area = SignedArea(ring);
            if (area > 0) outers.Add(([.. ring], area));
            else if (area < 0) holes.Add([.. ring]);
        }
        if (outers.Count == 0) return BucketFillRegion.Empty;
        var groups = new List<Vector2[]>[outers.Count];
        for (int i = 0; i < outers.Count; i++)
            groups[i] = [outers[i].Points];
        foreach (var hole in holes)
        {
            int parent = -1;
            double smallest = double.PositiveInfinity;
            for (int i = 0; i < outers.Count; i++)
            {
                double area = outers[i].Area;
                if (area < smallest && Geometry2D.IsPointInPolygon(hole[0], outers[i].Points))
                {
                    parent = i;
                    smallest = area;
                }
            }
            // Float conversion can collapse a very thin outer contour.
            if (parent < 0) return BucketFillRegion.Empty;
            groups[parent].Add(hole);
        }
        var polygons = ImmutableArray.CreateBuilder<ImmutableArray<ImmutableArray<Vector2>>>(groups.Length);
        foreach (var group in groups)
        {
            var rings = ImmutableArray.CreateBuilder<ImmutableArray<Vector2>>(group.Count);
            foreach (var boundary in group)
                rings.Add([.. boundary, boundary[0]]);
            polygons.Add(rings.MoveToImmutable());
        }
        return new BucketFillRegion(polygons.MoveToImmutable());

        bool IsBoundary(int edge) => selected[edge / 3] &&
            (_mesh.Opposites[edge] < 0 || !selected[_mesh.Opposites[edge] / 3]);
    }

    private static double SignedArea(IReadOnlyList<Vector2> ring)
    {
        double area = 0;
        var origin = ring[0];
        for (int i = 1; i + 1 < ring.Count; i++)
            area += ((double)ring[i].X - origin.X) * ((double)ring[i + 1].Y - origin.Y)
                - ((double)ring[i].Y - origin.Y) * ((double)ring[i + 1].X - origin.X);
        return area * 0.5;
    }

    public void Dispose() => _mesh.Dispose();
}
