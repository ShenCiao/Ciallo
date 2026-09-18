using System.Collections.Generic;
using System.Collections.Immutable;
using Ciallo.Data;
using Ciallo.Geometry;
using Frent;
using Godot;

namespace Ciallo.Tool;

public readonly record struct PaintStrokeSnapTarget(
    Entity Curve,
    Vector2 HitPoint,
    float HitT);

public sealed class PaintStrokeSnap
{
    private const float Epsilon = 1e-5f;
    private const float BodyTargetOverrunDistanceWorld = 0.05f;
    private const float EndpointPreferenceSnapDistanceRatio = 1f / 3;
    private const double RampTargetWeight = 0.01;
    private readonly HashSet<Entity> _seen = [];

    public PaintStrokeSnapTarget? TryFindTarget(
        Arrangement arr,
        Vector2 worldPosition,
        float snapDistance)
    {
        if (snapDistance <= 0f)
            return null;

        float snapDistanceSquared = snapDistance * snapDistance;
        float endpointPreferenceDistance = snapDistance * EndpointPreferenceSnapDistanceRatio;
        float endpointPreferenceDistanceSquared = endpointPreferenceDistance * endpointPreferenceDistance;
        var endpointTarget = default(PaintStrokeSnapTarget);
        var bodyTarget = default(PaintStrokeSnapTarget);
        float bestEndpointDistanceSquared = endpointPreferenceDistanceSquared;
        float bestBodyDistanceSquared = snapDistanceSquared;
        bool foundEndpoint = false;
        bool foundBody = false;
        _seen.Clear();

        // Although single-point strokes are theoretically valid snap targets, but Arrangement curve queries cannot return them, so we discard them intentionally.
        foreach (long targetId in arr.PolylineQueryCurves(PolylineShapeBuilder.BuildClosedOctagon(worldPosition, snapDistance)))
        {
            var curve = targetId.ToEntity();
            if (_seen.Add(curve))
                KeepIfNearer(curve);
        }

        if (foundEndpoint)
            return endpointTarget;
        if (foundBody)
            return bodyTarget;
        return null;

        void KeepIfNearer(Entity curve)
        {
            var positions = curve.Get<SampledPolyline>().Positions.Value;

            // Prefer curve endpoints only in the tighter endpoint-preference range.
            // Body targets can still use the full snap distance.
            float startDistSq = worldPosition.DistanceSquaredTo(positions[0]);
            float endDistSq = worldPosition.DistanceSquaredTo(positions[^1]);
            bool startInRange = startDistSq <= endpointPreferenceDistanceSquared;
            bool endInRange = endDistSq <= endpointPreferenceDistanceSquared;

            if (startInRange || endInRange)
            {
                bool useStart = startInRange && (!endInRange || startDistSq <= endDistSq);
                var endpointPoint = useStart ? positions[0] : positions[^1];
                float endpointT = useStart ? 0f : positions.Length - 1f;
                float endpointDistSq = useStart ? startDistSq : endDistSq;

                if (!foundEndpoint || endpointDistSq <= bestEndpointDistanceSquared)
                {
                    endpointTarget = new PaintStrokeSnapTarget(curve, endpointPoint, endpointT);
                    bestEndpointDistanceSquared = endpointDistSq;
                    foundEndpoint = true;
                }
                return;
            }

            var hitPoint = positions.GetClosestPoint(worldPosition, out var t);
            float distanceSquared = worldPosition.DistanceSquaredTo(hitPoint);
            if (distanceSquared > bestBodyDistanceSquared)
                return;

            bodyTarget = new PaintStrokeSnapTarget(curve, hitPoint, t);
            bestBodyDistanceSquared = distanceSquared;
            foundBody = true;
        }
    }

    public static PolylineSamples BuildRepairedGeometry(
        Arrangement arr,
        PolylineSamples geometry,
        PaintStrokeSnapTarget? startTarget,
        PaintStrokeSnapTarget? endTarget,
        float snapDistance)
    {
        if (geometry.Count == 0)
            return new PolylineSamples([], [], []);

        if (geometry.Count == 1)
        {
            var position = startTarget.HasValue
                ? ResolveRepairPoint(geometry.Positions, startTarget.Value, repairStart: true)
                : endTarget.HasValue
                    ? ResolveRepairPoint(geometry.Positions, endTarget.Value, repairStart: false)
                    : geometry.Positions[0];
            return CreateGeometry(geometry, [position]);
        }

        if (!startTarget.HasValue && !endTarget.HasValue)
            return CreateGeometry(geometry, geometry.Positions);

        var repairedPositions = new Vector2[geometry.Count];
        for (int i = 0; i < geometry.Count; i++)
            repairedPositions[i] = geometry.Positions[i];

        // Pierce judgment from the arrangement: query the ORIGINAL (un-committed) stroke and find, per
        // endpoint, the entry crossing T into the snap target (the crossing nearest that endpoint's tip).
        // The crossing is attributed to the endpoint in whose half of the stroke it falls, so a U drawn
        // over a bar can pierce both arms independently.
        var (startEntryT, endEntryT) = QueryPierceEntries(arr, geometry, startTarget, endTarget, snapDistance);

        var fixedDisplacements = new Dictionary<int, Vector2>(2);
        var rampOrigins = new List<int>(2);
        if (startTarget.HasValue)
        {
            if (startEntryT.HasValue)
                // Already crosses; pin the tip so the Laplacian ramp from the other end cannot drift it,
                // keeping the entry crossing T valid for the trim below.
                fixedDisplacements[0] = Vector2.Zero;
            else
            {
                var repairPoint = ResolveRepairPoint(geometry.Positions, startTarget.Value, repairStart: true);
                fixedDisplacements[0] = repairPoint - repairedPositions[0];
                rampOrigins.Add(0);
            }
        }
        if (endTarget.HasValue)
        {
            int endpoint = geometry.Count - 1;
            if (endEntryT.HasValue)
                fixedDisplacements[endpoint] = Vector2.Zero;
            else
            {
                var repairPoint = ResolveRepairPoint(geometry.Positions, endTarget.Value, repairStart: false);
                fixedDisplacements[endpoint] = repairPoint - repairedPositions[endpoint];
                rampOrigins.Add(endpoint);
            }
        }

        // Non-piercing snap endpoints use displacement-Laplacian repair so the endpoint move is absorbed
        // by the nearby stroke instead of only editing one sample. Pierced endpoints are pinned, so the
        // deformed array still matches the original near them and the entry T stays valid.
        IReadOnlyList<Vector2> deformed = rampOrigins.Count == 0
            ? repairedPositions
            : PolylineExtension.SolveDisplacementLaplacian(
                repairedPositions, fixedDisplacements, rampOrigins, RampTargetWeight);

        if (!startEntryT.HasValue && !endEntryT.HasValue)
            return CreateGeometry(geometry, deformed);

        // Trim the dangling tail past each pierce, keeping a small overrun on the OUTSIDE of the target
        // (retreat from the entry crossing toward the tip) so the committed stroke crosses cleanly.
        float maxT = geometry.Count - 1f;
        float fromT = startEntryT.HasValue
            ? deformed.MoveTByDistance(startEntryT.Value, BodyTargetOverrunDistanceWorld, forward: false)
            : 0f;
        float toT = endEntryT.HasValue
            ? deformed.MoveTByDistance(endEntryT.Value, BodyTargetOverrunDistanceWorld, forward: true)
            : maxT;

        return CreateTrimmedGeometry(geometry, deformed, fromT, toT);
    }

    private static PolylineSamples CreateTrimmedGeometry(
        PolylineSamples geometry,
        IReadOnlyList<Vector2> deformedPositions,
        float fromT,
        float toT)
    {
        // All three arrays share one sampling sequence (index i is the same sample across them), so the
        // same [fromT, toT] slices them consistently. Only positions were deformed; the rest are original.
        return new PolylineSamples(
            deformedPositions.ToImmutableArray().Slice(fromT, toT),
            geometry.Pressures.ToImmutableArray().Slice(fromT, toT),
            geometry.Tilts.ToImmutableArray().Slice(fromT, toT));
    }

    private static PolylineSamples CreateGeometry(
        PolylineSamples geometry,
        IReadOnlyList<Vector2> positions)
    {
        var repairedPositions = ImmutableArray.CreateBuilder<Vector2>(geometry.Count);

        for (int i = 0; i < geometry.Count; i++)
        {
            repairedPositions.Add(positions[i]);
        }

        return new PolylineSamples(
            repairedPositions.MoveToImmutable(),
            geometry.Pressures.ToImmutableArray(),
            geometry.Tilts.ToImmutableArray());
    }

    // Pierce judgment: query the original stroke against the arrangement and, per endpoint, find the
    // entry crossing into that endpoint's snap target — the crossing nearest the tip (min QueryT for the
    // start, max QueryT for the end). A crossing is attributed to the endpoint in whose half it falls.
    // Returns null for an endpoint that does not pierce.
    private static (float? StartEntryT, float? EndEntryT) QueryPierceEntries(
        Arrangement arr,
        PolylineSamples geometry,
        PaintStrokeSnapTarget? startTarget,
        PaintStrokeSnapTarget? endTarget,
        float snapDistance)
    {
        if (arr == null)
            return (null, null);

        var polyline = ImmutableArray.CreateRange(geometry.Positions);
        var intersections = arr.PolylineQueryCurveIntersections(polyline);
        if (intersections.Length == 0)
            return (null, null);

        // A crossing only counts as THIS snap's pierce when it sits near the snap hit point. Without this
        // the stroke crossing the same curve elsewhere in the half (a far wiggle) would anchor the entry
        // T far from the tip and trim away a large run of intended geometry.
        float snapLocalRadiusSq = snapDistance * snapDistance;
        float mid = (geometry.Count - 1) * 0.5f;
        float? startEntryT = null;
        float? endEntryT = null;
        foreach (var hit in intersections)
        {
            if (startTarget.HasValue && hit.SourceShape == startTarget.Value.Curve && hit.QueryT <= mid
                && hit.Position.DistanceSquaredTo(startTarget.Value.HitPoint) <= snapLocalRadiusSq)
                startEntryT = startEntryT is { } s ? Mathf.Min(s, hit.QueryT) : hit.QueryT;
            if (endTarget.HasValue && hit.SourceShape == endTarget.Value.Curve && hit.QueryT >= mid
                && hit.Position.DistanceSquaredTo(endTarget.Value.HitPoint) <= snapLocalRadiusSq)
                endEntryT = endEntryT is { } e ? Mathf.Max(e, hit.QueryT) : hit.QueryT;
        }
        return (startEntryT, endEntryT);
    }

    // Non-pierced overrun: push the snapped endpoint just across the target curve so the commit
    // produces a crossing. The endpoint lands on the side OPPOSITE the stroke's body bulk; since the
    // stroke does not cross the target near the snap (pierce was ruled out by QueryPierceEntries), the
    // body bulk is simply the side of the first off-line neighbour.
    private static Vector2 ResolveRepairPoint(
        IReadOnlyList<Vector2> strokePositions,
        PaintStrokeSnapTarget target,
        bool repairStart)
    {
        var targetPositions = target.Curve.Get<SampledPolyline>().Positions.Value;
        var hitPoint = targetPositions.Sample(target.HitT);
        var t = target.HitT;
        // Endpoint snap targets land exactly on the endpoint; no overrun needed.
        if (t <= Epsilon)
            return targetPositions[0];
        if (t >= targetPositions.Length - 1f - Epsilon)
            return targetPositions[^1];

        int segIndex = Mathf.Min((int)t, targetPositions.Length - 2);
        var segDir = targetPositions[segIndex + 1] - targetPositions[segIndex];
        var normal = new Vector2(segDir.Y, -segDir.X).Normalized();

        int endpoint = repairStart ? 0 : strokePositions.Count - 1;
        int step = repairStart ? 1 : -1;
        float bodyBulkSd = 0f;
        for (int i = endpoint + step; i >= 0 && i < strokePositions.Count; i += step)
        {
            float sd = (strokePositions[i] - hitPoint).Dot(normal);
            if (Mathf.Abs(sd) > Epsilon)
            {
                bodyBulkSd = sd;
                break;
            }
        }
        float bodyBulkSide = bodyBulkSd >= 0f ? 1f : -1f;
        return hitPoint - bodyBulkSide * normal * BodyTargetOverrunDistanceWorld;
    }
}
