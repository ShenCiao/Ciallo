using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;

namespace Ciallo.Geometry;

public sealed class BucketFillRegion(ImmutableArray<ImmutableArray<ImmutableArray<Vector2>>> polygons)
{
    public static readonly BucketFillRegion Empty = new([]);

    // [polygon][ring][point]: each polygon has a closed CCW outer ring followed by closed CW holes.
    public ImmutableArray<ImmutableArray<ImmutableArray<Vector2>>> Polygons { get; } = polygons;
    public IEnumerable<ImmutableArray<Vector2>> Contours => Polygons.SelectMany(polygon => polygon);
    public bool IsEmpty => Polygons.IsEmpty;
}
