using System;
using Godot;

namespace Ciallo.Rendering;

/// <summary>Shared settings and materials for polygon fills and their previews.</summary>
public static class PolygonFillRendering
{
    internal static readonly ShaderMaterial SolidMaterial = new()
    {
        Shader = GD.Load<Shader>("res://Rendering/PolygonFill.gdshader"),
    };
    internal static readonly ShaderMaterial HintMaterial =
        (ShaderMaterial)GD.Load<ShaderMaterial>("res://Rendering/FillHint.tres").Duplicate();

    private static bool _aaEnabled = true;
    private static float _aaEpsilon = 0.01f;
    private static float _aaMaxVertexOffsetPixels = 10f;
    internal static int BoundsRevision { get; private set; }

    public static bool AaEnabled
    {
        get => _aaEnabled;
        set
        {
            _aaEnabled = value;
            SetParameter("AaEnabled", value);
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
            SetParameter("AaEpsilon", value);
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
            SetParameter("AaMaxVertexOffsetPixels", value);
            BoundsRevision++;
        }
    }

    static PolygonFillRendering()
    {
        SetParameter("AaEnabled", _aaEnabled);
        SetParameter("AaEpsilon", _aaEpsilon);
        SetParameter("AaMaxVertexOffsetPixels", _aaMaxVertexOffsetPixels);
    }

    private static void SetParameter(StringName name, Variant value)
    {
        SolidMaterial.SetShaderParameter(name, value);
        HintMaterial.SetShaderParameter(name, value);
    }
}
