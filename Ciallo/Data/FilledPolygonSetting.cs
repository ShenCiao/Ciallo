using System;
using System.Runtime.Serialization;
using Frent;
using R3;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class FilledPolygonSetting : IEquatable<FilledPolygonSetting>
{
    // TODO: Add a Holes field here for fill-only inner contours; keep the outer contour in SampledPolyline.
    // Polygon-to-stroke/polyline conversion intentionally uses only the outer contour.
    // Hole support must include serialization, cloning/equality, geometry edits/transforms, rendering, and hit testing.
    // Until then, holes remain connected into the single SampledPolyline ring by duplicated bridge edges.

    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Nullable)]
    public ReactiveProperty<Entity> BrushE = new(default);

    public FilledPolygonSetting Clone()
    {
        return new FilledPolygonSetting
        {
            BrushE = { Value = BrushE.Value },
        };
    }

    public bool Equals(FilledPolygonSetting other)
    {
        if (ReferenceEquals(other, null)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(BrushE.Value, other.BrushE.Value);
    }

    public override bool Equals(object obj) => obj is FilledPolygonSetting other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(BrushE.Value);
}
