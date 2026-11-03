using System.Collections.Generic;
using Godot;

namespace Ciallo.Geometry;

/// <summary>Aligned path samples before brush mappings are applied.</summary>
public readonly record struct PolylineSamples(
    IReadOnlyList<Vector2> Positions,
    IReadOnlyList<float> Pressures,
    IReadOnlyList<Vector2> Tilts)
{
    public int Count => Positions.Count;

    public static PolylineSamples Uniform(IReadOnlyList<Vector2> positions, float pressure = 1f)
    {
        var pressures = new float[positions.Count];
        System.Array.Fill(pressures, pressure);
        return new(positions, pressures, new Vector2[positions.Count]);
    }
}
