using Ciallo.Data;
using Godot;

namespace Ciallo.Rendering;

public partial class FillBrushMaterial : ShaderMaterial
{
    public Color Color
    {
        get => GetShaderParameter("FillColor").AsColor();
        set => SetShaderParameter("FillColor", value);
    }

    public FillBrushFlags ActiveBrushFlags
    {
        get => (FillBrushFlags)GetShaderParameter("ActiveBrushFlags").AsInt32();
        set => SetShaderParameter("ActiveBrushFlags", (int)value);
    }

    public FillBrushMaterial() : this(Colors.White) { }

    public FillBrushMaterial(Color color)
    {
        Shader = GD.Load<Shader>("res://Rendering/PolygonFill.gdshader");
        Color = color;
    }
}
