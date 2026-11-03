using System;
using Godot;

namespace Ciallo.Rendering;

/// <summary>Shared settings and materials for polygon fills and their previews.</summary>
public static class PolygonFillRendering
{
    internal static readonly FillBrushMaterial WhiteMaterial = new();
    internal static readonly ShaderMaterial HintMaterial =
        (ShaderMaterial)GD.Load<ShaderMaterial>("res://Rendering/FillHint.tres").Duplicate();

    private static bool _aaEnabled = ProjectSettings.GetSetting("shader_globals/PolygonFillAaEnabled").AsGodotDictionary()["value"].AsBool();
    private static float _aaEpsilon = ProjectSettings.GetSetting("shader_globals/PolygonFillAaEpsilon").AsGodotDictionary()["value"].AsSingle();
    private static float _aaMaxVertexOffsetPixels = ProjectSettings.GetSetting("shader_globals/PolygonFillAaMaxVertexOffsetPixels").AsGodotDictionary()["value"].AsSingle();
    internal static int BoundsRevision { get; private set; }

    public static bool AaEnabled
    {
        get => _aaEnabled;
        set
        {
            _aaEnabled = value;
            RenderingServer.GlobalShaderParameterSet("PolygonFillAaEnabled", value);
            BoundsRevision++;
        }
    }

    public static float AaEpsilon
    {
        get => _aaEpsilon;
        set
        {
            if (!float.IsFinite(value) || value <= 0f || value >= 1f)
                throw new ArgumentOutOfRangeException(nameof(value), "AA epsilon must be between zero and one.");
            _aaEpsilon = value;
            RenderingServer.GlobalShaderParameterSet("PolygonFillAaEpsilon", value);
        }
    }

    public static float AaMaxVertexOffsetPixels
    {
        get => _aaMaxVertexOffsetPixels;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), "The vertex offset must be finite and nonnegative.");
            _aaMaxVertexOffsetPixels = value;
            RenderingServer.GlobalShaderParameterSet("PolygonFillAaMaxVertexOffsetPixels", value);
            BoundsRevision++;
        }
    }
}
