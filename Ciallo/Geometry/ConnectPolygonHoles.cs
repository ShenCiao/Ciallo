using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Ciallo.Geometry;

public static class ConnectPolygonHoles
{
    /// <summary>
    /// Merges holes into the outer polygon by inserting bridge edges.
    /// The result is a weakly simple ring with duplicated bridge vertices, suitable for polygon repair.
    /// </summary>
    /// <param name="polygonWithHoles">First array is outer polygon (CCW), other arrays are holes (CW)</param>
    /// <returns>Single merged polygon ring</returns>
    public static List<Vector2> ConnectHoles(this IReadOnlyList<IReadOnlyList<Vector2>> polygonWithHoles)
    {
        var merged = new List<Vector2>(polygonWithHoles[0]);

        // Rightward bridges must not cross holes that have not been connected yet.
        foreach (var hole in polygonWithHoles.Skip(1).Where(hole => hole.Count > 0)
                     .OrderByDescending(hole => hole.Max(point => point.X)))
        {
            merged = ConnectOneHole(merged, hole);
        }

        return merged;
    }

    /// <summary>
    /// Connect a single hole into the current outer ring and return the merged ring.
    /// </summary>
    private static List<Vector2> ConnectOneHole(List<Vector2> outer, IReadOnlyList<Vector2> hole)
    {
        // Step 1: find the rightmost vertex of the hole.
        int holeMaxIdx = 0;
        for (int i = 1; i < hole.Count; i++)
        {
            if (hole[i].X > hole[holeMaxIdx].X ||
                (hole[i].X == hole[holeMaxIdx].X && hole[i].Y < hole[holeMaxIdx].Y))
                holeMaxIdx = i;
        }
        Vector2 holeVtx = hole[holeMaxIdx];

        // Step 2: cast a ray from holeVtx in the +X direction and find the closest
        //         intersection with any edge of the outer ring.
        int outerEdgeIdx = -1; // start index of the best outer edge
        double bestT = double.MaxValue; // parameter along the outer edge
        double bestX = double.MaxValue; // x of the intersection

        int outerCount = outer.Count;
        for (int i = 0; i < outerCount; i++)
        {
            Vector2 a = outer[i];
            Vector2 b = outer[(i + 1) % outerCount];

            // Only consider edges whose y-range straddles holeVtx.Y
            // (strictly to avoid double-counting shared vertices)
            float minY = MathF.Min(a.Y, b.Y);
            float maxY = MathF.Max(a.Y, b.Y);
            if (holeVtx.Y < minY || holeVtx.Y >= maxY) continue;

            // Compute x of the intersection of the edge with the horizontal ray y = holeVtx.Y
            double dy = (double)b.Y - a.Y;
            double t = ((double)holeVtx.Y - a.Y) / dy;
            double xIntersect = a.X + t * ((double)b.X - a.X);

            // Only intersections to the right of (or exactly at) holeVtx
            if (xIntersect < holeVtx.X) continue;

            if (xIntersect < bestX)
            {
                bestX = xIntersect;
                bestT = t;
                outerEdgeIdx = i;
            }
        }

        // Step 3: determine the insertion point on the outer ring.
        // If the intersection is exactly a vertex of the outer ring, use that vertex.
        // Otherwise, split the edge by inserting the intersection point, then use it.
        int insertIdx = outer.IndexOf(holeVtx);
        if (insertIdx < 0)
        {
            if (outerEdgeIdx < 0)
                throw new InvalidOperationException("Hole has no bridge to its containing contour.");
            int nextOuter = (outerEdgeIdx + 1) % outerCount;
            if (bestT == 1) insertIdx = nextOuter;
            else if (bestT == 0) insertIdx = outerEdgeIdx;
            else
            {
                outer.Insert(nextOuter, new Vector2((float)bestX, holeVtx.Y));
                insertIdx = nextOuter;
            }
        }

        // Step 4: build the merged ring.
        // The bridge goes:  ... outer[0..insertIdx], holeVtx, hole (rotated), holeVtx, outer[insertIdx], ...
        // i.e. we insert the hole starting at holeMaxIdx and wrap around back to holeMaxIdx,
        // then repeat outer[insertIdx] to close the bridge.
        var result = new List<Vector2>(outer.Count + hole.Count + 2);

        // Outer vertices up to and including the bridge point
        for (int i = 0; i <= insertIdx; i++)
            result.Add(outer[i]);

        // Hole vertices starting from holeMaxIdx, going around the whole hole
        for (int i = 0; i <= hole.Count; i++)
            result.Add(hole[(holeMaxIdx + i) % hole.Count]);

        // Bridge back: repeat the outer bridge point
        result.Add(outer[insertIdx]);

        // Remaining outer vertices
        for (int i = insertIdx + 1; i < outer.Count; i++)
            result.Add(outer[i]);

        return result;
    }
}
