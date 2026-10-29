using Ciallo.Data;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

internal static class BrushPreview
{
    public static Control Stroke(Entity e)
    {
        var wrapper = new PanelContainer()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        var checkerboard = new ColorRect
        {
            Material = AutoloadRendering.CheckerboardMaterial,
            Color = Colors.Transparent,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        wrapper.AddChild(checkerboard);

        var textureRect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = e.Get<ViewportTexture>(),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        wrapper.AddChild(textureRect);

        var nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
        };
        nameLabel.AddThemeConstantOverride("outline_size", 8);
        var nameWrapper = new MarginContainer()
        {
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        nameWrapper.AddThemeConstantOverride("margin_bottom", 12);
        nameWrapper.AddThemeConstantOverride("margin_right", 8);
        nameWrapper.AddChild(nameLabel);
        wrapper.AddChild(nameWrapper);

        var setting = e.Get<StrokeBrushSetting>();
        setting.Name.Subscribe(name => nameLabel.Text = name).AddTo(wrapper);


        return wrapper;
    }

    public static PanelContainer Fill(Entity e)
    {
        var box = new PanelContainer();
        var background = new ColorRect() { Material = AutoloadRendering.CheckerboardMaterial };
        box.AddChild(background);
        var markerPreview = new TextureRect()
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            CustomMinimumSize = new(32, 32),
        };
        var container = new CenterContainer();
        container.AddChild(markerPreview);
        box.AddChild(container);

        var nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            AnchorRight = 1,
            OffsetLeft = 4,
            OffsetRight = -4,
            OffsetTop = 16,
            OffsetBottom = 16,
            GrowVertical = Control.GrowDirection.Both,
        };
        nameLabel.AddThemeConstantOverride("outline_size", 8);
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        var nameOverlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        nameOverlay.AddChild(nameLabel);
        box.AddChild(nameOverlay);

        var setting = e.Get<FillBrushSetting>();
        setting.Name.Subscribe(name => nameLabel.Text = name).AddTo(box);
        setting.MarkerTexture.Subscribe(markerPreview.SetTexture).AddTo(box);
        setting.MarkerColor.Subscribe(markerPreview.SetSelfModulate).AddTo(box);
        setting.FillColor.Subscribe(background.SetColor).AddTo(box);
        return box;
    }

    public static Control Missing(string text)
    {
        var box = new PanelContainer();
        box.AddChild(new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        return box;
    }
}