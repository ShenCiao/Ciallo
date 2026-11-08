using Godot;

namespace Ciallo.Rendering;

public partial class FillBrushMaterial : ShaderMaterial
{
    public Color? Color
    {
        get
        {
            var color = GetShaderParameter("FillColor").AsColor();
            return color.A < 0 ? null : color;
        }
        // Negative alpha encodes no override at the shader boundary.
        set => SetShaderParameter("FillColor", value ?? new Color(0, 0, 0, -1));
    }

    public FillBrushMaterial() : this(Colors.White) { }

    public FillBrushMaterial(Color? color)
    {
        Shader = GD.Load<Shader>("res://Rendering/PolygonFill.gdshader");
        Color = color;
    }
}
