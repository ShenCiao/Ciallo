using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Ciallo.Geometry;

public static partial class PolylineExtension
{
    /// <summary>
    /// Simplifies an open polyline with the Ramer-Douglas-Peucker algorithm.
    /// Points are removed when their perpendicular distance to the current range's
    /// endpoint line and optional radius interpolation error are within
    /// <paramref name="tolerance"/>, and the optional source segment length
    /// constraint allows the removal.
    /// </summary>
    /// <param name="polyline">Input polyline points.</param>
    /// <param name="tolerance">Position and radius error threshold in the same units as <paramref name="polyline"/>.</param>
    /// <param name="originalIndex">Indices of kept points in the original polyline.</param>
    /// <param name="maxSegmentLength">Maximum original polyline path length allowed between two kept points. Values less than or equal to zero disable this constraint.</param>
    /// <param name="radii">Optional point radii in the same units as the positions, checked against the same tolerance.</param>
    /// <returns>Simplified polyline.</returns>
    public static List<Vector2> SimplifyRdp(
        this IReadOnlyList<Vector2> polyline,
        float tolerance,
        out List<int> originalIndex,
        float maxSegmentLength = 0f,
        IReadOnlyList<float> radii = null)
    {
        int count = polyline.Count;
        if (count <= 2 || tolerance <= 0f)
        {
            originalIndex = Enumerable.Range(0, count).ToList();
            return polyline.ToList();
        }

        bool hasMaxSegmentLength = maxSegmentLength > 0f;
        float[] cumulativeLengths = [];
        if (hasMaxSegmentLength)
        {
            cumulativeLengths = new float[count];
            for (int i = 1; i < count; i++)
                cumulativeLengths[i] = cumulativeLengths[i - 1] + polyline[i - 1].DistanceTo(polyline[i]);
        }

        var deleted = new bool[count];
        var stack = new Stack<(int First, int Last)>();
        stack.Push((0, count - 1));

        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            if (last - first < 2)
                continue;

            float maxDistance = -1f;
            int maxIndex = -1;

            for (int i = first + 1; i < last; i++)
            {
                float distance = SimplificationError(polyline, radii, first, last, i);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = i;
                }
            }

            if (maxDistance > tolerance)
            {
                stack.Push((first, maxIndex));
                stack.Push((maxIndex, last));
            }
            else if (hasMaxSegmentLength && cumulativeLengths[last] - cumulativeLengths[first] > maxSegmentLength)
            {
                int middle = FindSegmentMiddle(cumulativeLengths, first, last);
                stack.Push((first, middle));
                stack.Push((middle, last));
            }
            else
            {
                for (int i = first + 1; i < last; i++)
                    deleted[i] = true;
            }
        }

        var result = new List<Vector2>(count);
        originalIndex = [];
        for (int i = 0; i < count; i++)
        {
            if (deleted[i])
                continue;

            result.Add(polyline[i]);
            originalIndex.Add(i);
        }

        return result;
    }

    private static int FindSegmentMiddle(IReadOnlyList<float> cumulativeLengths, int first, int last)
    {
        float target = (cumulativeLengths[first] + cumulativeLengths[last]) * 0.5f;
        int middle = first + 1;
        while (middle < last - 1 && cumulativeLengths[middle] < target)
            middle++;

        return middle;
    }

    private static float SimplificationError(
        IReadOnlyList<Vector2> polyline, IReadOnlyList<float> radii, int first, int last, int index)
    {
        var from = polyline[first];
        var to = polyline[last];
        var value = polyline[index];
        var ray = to - from;

        float lambda = 0f;
        float rayLengthSquared = ray.LengthSquared();
        if (rayLengthSquared > 0f)
            lambda = ray.Dot(value - from) / rayLengthSquared;

        var interpolated = from.Lerp(to, lambda);
        float distance = value.DistanceTo(interpolated);
        return radii is null
            ? distance
            : MathF.Max(distance, MathF.Abs(radii[index] - Mathf.Lerp(radii[first], radii[last], lambda)));
    }

    /// <summary>
    /// A variant of Visvalingam–Whyatt with Curvature-weighted distance metric.
    /// Prefer removing points on straight segments, keep dense points in corners.
    /// </summary>
    /// <remarks>
    /// Retained for possible future exposure as a user-selectable algorithm.
    /// The selection tool currently uses <see cref="SimplifyRdp"/>.
    /// </remarks>
    /// <param name="polyline">Input polyline points.</param>
    /// <param name="simplificationRatio">
    /// Fraction of points to remove, in [0,1].
    /// 0 keeps original, 1 leaves the minimum (2) points.
    /// </param>
    /// <param name="originalIndex">Indices of kept points in the original polyline.</param>
    /// <returns>Simplified polyline.</returns>
    public static List<Vector2> SimplifyCurvatureDistance(this IReadOnlyList<Vector2> polyline, float simplificationRatio, out List<int> originalIndex)
    {
        int count = polyline.Count;
        float ratio = 1f - simplificationRatio;

        if (count <= 2 || ratio >= 1f)
        {
            originalIndex = Enumerable.Range(0, count).ToList();
            return polyline.ToList();
        }

        if (ratio <= 0f)
            ratio = 0f;

        int targetCount = (int)MathF.Round(count * ratio);
        if (targetCount < 2) targetCount = 2;
        if (targetCount > count) targetCount = count;
        if (targetCount == count)
        {
            originalIndex = Enumerable.Range(0, count).ToList();
            return polyline.ToList();
        }

        var prev = new int[count];
        var next = new int[count];
        var removed = new bool[count];
        var importance = new float[count];

        for (int i = 0; i < count; i++)
        {
            prev[i] = i - 1;
            next[i] = i + 1;
        }

        next[count - 1] = count;

        float PointImportance(int i)
        {
            int p = prev[i];
            int n = next[i];
            if (p < 0 || n >= count) return float.PositiveInfinity;

            var a = polyline[p];
            var b = polyline[i];
            var c = polyline[n];

            var ab = b - a;
            var bc = c - b;
            var ac = c - a;

            if (ab.IsZeroApprox() || bc.IsZeroApprox() || ac.IsZeroApprox())
                return float.PositiveInfinity;

            // perpendicular distance from b to line a-c
            float dist = b.DistanceToLine(a, ac.Normalized());

            return dist / Mathf.Log(Mathf.Max(1e-5f, ab.Length() + bc.Length()));
        }

        var heap = new PriorityQueue<int, float>();
        for (int i = 1; i < count - 1; i++)
        {
            importance[i] = PointImportance(i);
            heap.Enqueue(i, importance[i]);
        }

        int remaining = count;

        while (remaining > targetCount && heap.Count > 0)
        {
            int i = heap.Dequeue();
            if (removed[i]) continue;

            float currentImportance = PointImportance(i);
            if (MathF.Abs(currentImportance - importance[i]) > 1e-6f)
            {
                importance[i] = currentImportance;
                heap.Enqueue(i, importance[i]);
                continue;
            }

            removed[i] = true;
            remaining--;

            int p = prev[i];
            int n = next[i];

            if (p >= 0) next[p] = n;
            if (n < count) prev[n] = p;

            if (p > 0 && p < count - 1 && !removed[p])
            {
                importance[p] = PointImportance(p);
                heap.Enqueue(p, importance[p]);
            }

            if (n > 0 && n < count - 1 && !removed[n])
            {
                importance[n] = PointImportance(n);
                heap.Enqueue(n, importance[n]);
            }
        }

        var result = new List<Vector2>();
        originalIndex = [];
        int idx = 0;

        while (idx < count)
        {
            if (!removed[idx])
            {
                result.Add(polyline[idx]);
                originalIndex.Add(idx);
            }

            idx = next[idx];
            if (idx >= count) break;
        }

        return result;
    }
}
