using Godot;

namespace Ciallo.Rendering;

public partial class FillBrushMaterial : ShaderMaterial
{
    public Color Color
    {
        get => GetShaderParameter("FillColor").AsColor();
        set => SetShaderParameter("FillColor", value);
    }

    public FillBrushMaterial() : this(Colors.White) { }

    public FillBrushMaterial(Color color)
    {
        Shader = GD.Load<Shader>("res://Rendering/PolygonFill.gdshader");
        Color = color;
    }
}
