using Godot;

namespace Ciallo.GuiControl;

internal sealed class LayerLabelColorStyle(ILayerBlock block)
{
    private const float HoverAmount = 0.125f;
    private static readonly StringName[] BackgroundSlots = ["normal", "pressed", "hover", "hover_pressed", "disabled"];
    // Binding precedes tree entry, so use the scene's declared theme instead of live Control lookups.
    private readonly StyleBoxFlat _normal = (StyleBoxFlat)block.Node.Theme.GetStylebox("normal", "LayerBlockButton").Duplicate();
    private readonly StyleBoxFlat _hover = (StyleBoxFlat)block.Node.Theme.GetStylebox("hover", "LayerBlockButton").Duplicate();

    public void Update(Color? markColor)
    {
        if (!block.Node.IsInsideTree()) return;

        if (!markColor.HasValue)
        {
            foreach (var slot in BackgroundSlots)
            {
                block.VisibleButton.RemoveThemeStyleboxOverride(slot);
                block.WorkingButton.RemoveThemeStyleboxOverride(slot);
            }
            block.VisibleButton.RemoveThemeColorOverride("checkbox_checked_color");
            block.VisibleButton.RemoveThemeColorOverride("checkbox_unchecked_color");
            block.WorkingButton.RemoveThemeColorOverride("button_checked_color");
            block.WorkingButton.RemoveThemeColorOverride("button_unchecked_color");
            return;
        }

        bool linear = block.Node.GetViewport().UseHdr2D;
        var background = Blend(GetBackgroundColor(linear), markColor.Value, linear);
        float luminance = background.SrgbToLinear().Luminance;
        bool useBlackIcon = (luminance + 0.05f) / 0.05f >= 1.05f / (luminance + 0.05f);
        var iconColor = useBlackIcon ? Colors.Black : Colors.White;

        _normal.BgColor = markColor.Value;
        // Derive hover from the visible color, including alpha, so even a transparent mark has feedback.
        // The small change keeps the chosen icon readable without switching it on hover.
        _hover.BgColor = useBlackIcon ? background.Darkened(HoverAmount) : background.Lightened(HoverAmount);
        foreach (var slot in BackgroundSlots)
        {
            var style = slot == "hover" || slot == "hover_pressed" ? _hover : _normal;
            block.VisibleButton.AddThemeStyleboxOverride(slot, style);
            block.WorkingButton.AddThemeStyleboxOverride(slot, style);
        }
        block.VisibleButton.AddThemeColorOverride("checkbox_checked_color", iconColor);
        block.VisibleButton.AddThemeColorOverride("checkbox_unchecked_color", iconColor);
        block.WorkingButton.AddThemeColorOverride("button_checked_color", iconColor);
        block.WorkingButton.AddThemeColorOverride("button_unchecked_color", iconColor);
    }

    private Color GetBackgroundColor(bool linear)
    {
        var background = Colors.Transparent;
        for (var parent = block.Node.GetParent(); parent != null; parent = parent.GetParent())
        {
            if (parent is not (Panel or PanelContainer or TabContainer or ScrollContainer)) continue;
            if (((Control)parent).GetThemeStylebox("panel") is not StyleBoxFlat { DrawCenter: true } panel) continue;
            background = Blend(panel.BgColor, background, linear);
            if (background.A >= 1f) return background;
        }
        return Blend(RenderingServer.GetDefaultClearColor(), background, linear);
    }

    // HDR 2D blends in linear space; theme colors and picker values are sRGB.
    private static Color Blend(Color background, Color foreground, bool linear) => linear
        ? background.SrgbToLinear().Blend(foreground.SrgbToLinear()).LinearToSrgb()
        : background.Blend(foreground);
}
