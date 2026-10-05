using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Godot;

namespace Ciallo.Geometry;

// Follows Blender Grease Pencil's CDT fill approach, based on "Delaunay Painting:
// Perceptual Image Colouring from Raster Contours with Gaps" by Amal Dev Parakkat,
// Pooran Memari, and Marie-Paule Cani (2022), https://doi.org/10.1111/cgf.14517.
// Blender reference: source/blender/editors/sculpt_paint/grease_pencil/fill.cc,
// delaunay_fill_strokes and add_weights_for_tri.
//
// The algorithm uses widest-path propagation and competing region seeds to separate gaps.
// With gap detection enabled, Ciallo adds a hard constraint: each artificial closing edge
// must touch at least one original endpoint of an open stroke. BucketFillGraph contracts
// all other unconstrained triangle adjacencies before competition, preserving narrow passages
// between stroke interiors while choosing among eligible closing edges. The contracted graph
// is built once per CDT snapshot and reused across fill queries.
public sealed class BucketFillSolver : IDisposable
{
    private readonly CdtMesh2D _mesh;
    private readonly BucketFillGraph _triangleGraph;
    private readonly BucketFillGraph _gapGraph;
    private int[] _outsideLabels;
    private double[] _outsideWeights;
    private int _cachedSeed = -1;
    private bool _cachedGapAware;
    private double _cachedGapFactor;
    private bool _cachedIgnoreHoles;
    private BucketFillRegion _cachedRegion;
    public int TriangleCount => _mesh.TriangleCount;

    public static BucketFillSolver Build(IReadOnlyList<ImmutableArray<Vector2>> strokes)
    {
        var vertices = new List<Vector2>();
        var constraints = new List<int>();
        var endpoints = new HashSet<(double X, double Y)>();
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
            if (stroke[0] != stroke[^1])
            {
                endpoints.Add((stroke[0].X, stroke[0].Y));
                endpoints.Add((stroke[^1].X, stroke[^1].Y));
            }
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
        return new BucketFillSolver(new CdtMesh2D([.. vertices], [.. constraints], frame), endpoints);
    }

    private BucketFillSolver(CdtMesh2D mesh, HashSet<(double X, double Y)> endpoints)
    {
        _mesh = mesh;
        _triangleGraph = BucketFillGraph.BuildTriangles(mesh);
        _gapGraph = BucketFillGraph.BuildGapRegions(mesh, endpoints);
    }

    public BucketFillRegion Query(Vector2 seed, bool gapAware, double gapFactor, bool ignoreHoles = false)
    {
        if (!double.IsFinite(gapFactor) || gapFactor is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(gapFactor), "Gap factor must be between zero and one.");
        int triangle = _mesh.Locate(seed);
        if (triangle < 0 || _triangleGraph.FrameNodes[triangle]) return BucketFillRegion.Empty;
        bool detectGaps = gapAware && gapFactor > 0;
        var graph = detectGaps ? _gapGraph : _triangleGraph;
        int start = graph.TriangleNodes[triangle];
        if (_cachedSeed == start && _cachedGapAware == gapAware && _cachedGapFactor == gapFactor &&
            _cachedIgnoreHoles == ignoreHoles) return _cachedRegion;
        int[] labels;
        double[] weights;
        int label = 1;
        if (detectGaps)
        {
            labels = new int[graph.Count];
            Array.Fill(labels, -1);
            weights = new double[graph.Count];
            graph.Propagate(start, 0, labels, weights);
            // GP seeds regions that are not reached at their local full capacity.
            while (true)
            {
                int next = -1;
                double best = 0;
                for (int i = 0; i < graph.Count; i++)
                    if (weights[i] < graph.Capacities[i] * gapFactor && weights[i] > best)
                    {
                        next = i;
                        best = weights[i];
                    }
                if (next < 0) break;
                graph.Propagate(next, label++, labels, weights);
            }
        }
        else
        {
            // Exterior competition is only used with gap detection disabled (or a zero factor).
            if (_outsideWeights == null)
            {
                _outsideLabels = new int[graph.Count];
                Array.Fill(_outsideLabels, -1);
                _outsideWeights = new double[graph.Count];
                int exterior = Array.FindIndex(graph.FrameNodes, value => value);
                graph.Propagate(exterior, 0, _outsideLabels, _outsideWeights);
            }
            labels = (int[])_outsideLabels.Clone();
            weights = (double[])_outsideWeights.Clone();
        }
        // The click wins equal-width competition in GP's final propagation pass.
        graph.Propagate(start, label, labels, weights);
        bool touchesFrame = false;
        for (int i = 0; i < graph.Count; i++)
        {
            if (labels[i] == label && graph.FrameNodes[i])
            {
                touchesFrame = true;
                break;
            }
        }
        // Rejected regions must also reach the cache, or hovering repeats the full propagation.
        var region = BucketFillRegion.Empty;
        if (!touchesFrame)
        {
            var selected = new bool[TriangleCount];
            for (int i = 0; i < selected.Length; i++) selected[i] = labels[graph.TriangleNodes[i]] == label;
            region = ExtractRegion(selected, ignoreHoles);
        }
        _cachedSeed = start;
        _cachedGapAware = gapAware;
        _cachedGapFactor = gapFactor;
        _cachedIgnoreHoles = ignoreHoles;
        return _cachedRegion = region;
    }

    private BucketFillRegion ExtractRegion(bool[] selected, bool ignoreHoles)
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
            else if (area < 0 && !ignoreHoles) holes.Add([.. ring]);
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
