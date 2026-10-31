using System.Collections.Generic;
using Ciallo.Data;
using Ciallo.Widget;
using Godot;
using Frent;

namespace Ciallo.Rendering;

public partial class VectorFillMarkerView : Node2D
{
    // Solid dot beneath the sprite hinting at the color the marker will fill with.
    public FillColorDot ColorDot = new()
    {
        Visible = false,
        Material = AutoloadRendering.VectorFillMarkerMaterial,
    };

    public FixedSizeSprite2D Sprite = new()
    {
        Visible = false,
        Material = AutoloadRendering.VectorFillMarkerMaterial,
    };

    public VectorFillMarkerView()
    {
        // ColorDot added first so it renders below the sprite.
        AddChild(ColorDot);
        AddChild(Sprite);
    }

    public void SetGeometry(
        IReadOnlyList<Vector2> positions,
        IReadOnlyList<float> radii)
    {
        if (positions.Count == 1)
        {
            var p = positions[0];
            var r = radii[0];
            Sprite.Visible = true;
            Sprite.Position = p;
            Sprite.Size = 2 * r * Vector2.One;
            ColorDot.Visible = true;
            ColorDot.Position = p;
            ColorDot.Radius = r;
            return;
        }
        Sprite.Visible = false;
        ColorDot.Visible = false;
    }

    public static void ApplyBrush(PolygonFillView polygon, VectorFillMarkerView marker, Entity brushE)
    {
        if (brushE.IsNull)
        {
            ApplyMissingBrush(polygon, marker);
            return;
        }

        ApplyBrush(polygon, marker, brushE.Get<FillBrushSetting>());
    }

    public static void ApplyBrush(PolygonFillView polygon, VectorFillMarkerView marker, FillBrushSetting setting)
    {
        polygon.Color = setting.FillColor.Value;
        polygon.MissingBrush = false;
        marker.Sprite.Texture = setting.MarkerTexture.Value ?? ImageTexture.Dummy;
        marker.Sprite.Modulate = setting.MarkerColor.Value;
        marker.ColorDot.Color = setting.FillColor.Value;
    }

    public static void ApplyMissingBrush(PolygonFillView polygon, VectorFillMarkerView marker)
    {
        polygon.Color = Colors.Black;
        polygon.MissingBrush = true;
        marker.Sprite.Texture = ImageTexture.Dummy;
        marker.Sprite.Modulate = Colors.Black;
        marker.ColorDot.Color = Colors.Black;
    }
}

/// <summary>
/// A solid circle centered at the local origin, drawn with the marker's zoom-compensating material
/// so it keeps a fixed screen size like <see cref="FixedSizeSprite2D"/>.
/// </summary>
public partial class FillColorDot : Node2D
{
    public float Radius
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    } = 1f;

    public Color Color
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    } = Colors.Black;

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, Color);
    }
}
