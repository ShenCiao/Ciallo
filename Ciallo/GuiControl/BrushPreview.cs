using Ciallo.Data;
using System;
using System.Linq;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

internal static class BrushPreview
{
    internal static readonly Vector2I StrokeBufferSize = new(256, 96);
    public static float StrokeAspectRatio => (float)StrokeBufferSize.X / StrokeBufferSize.Y;
    private const float MinPreviewRadius = 1f;
    private const float DefaultPreviewRadius = 13f;
    private const float MaxPreviewRadius = 36f;

    public static Control Stroke(Entity e)
        => Stroke(e.Get<StrokeBrushSetting>(), e.Get<ViewportTexture>());

    public static Control Stroke(StrokeBrushSetting setting)
    {
        var material = new StrokeBrushMaterial();
        material.ObserveBrushSetting(setting);
        var color = AppDocumentManager.WorkingDocument.Select(document => document.IsNull
            ? Observable.Return(Colors.Black) : document.Get<ColorManager>().CurrentColor.AsObservable()).Switch();
        var viewport = CreateStrokeViewport(setting, material, color, Observable.Return(BrushManager.DefaultStrokeRadius));
        var wrapper = Stroke(setting, viewport.GetTexture());
        wrapper.AddChild(viewport);
        material.Subs.AddTo(wrapper);
        return wrapper;
    }

    internal static SubViewport CreateStrokeViewport(StrokeBrushSetting setting, StrokeBrushMaterial material,
        Observable<Color> color, Observable<float> globalRadius, int sampleCount = 32)
    {
        var viewport = new SubViewport
        {
            Size = StrokeBufferSize, TransparentBg = true, UseHdr2D = true, Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
        };
        // Share the stroke's canvas so detail-preview pan/zoom exposes transparency beyond the sample.
        var background = new HBoxContainer { Size = StrokeBufferSize };
        background.AddThemeConstantOverride("separation", 0);
        background.AddChild(new ColorRect { Color = Colors.White, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        background.AddChild(new ColorRect { Color = new(0.5f, 0.5f, 0.5f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        viewport.AddChild(background);
        var stroke = new StrokeView { Material = material };
        viewport.AddChild(stroke);
        color.Subscribe(value => stroke.SetInstanceShaderParameter("ShapeColor", value)).AddTo(viewport);
        var pressures = Enumerable.Range(0, sampleCount)
            .Select(i => Mathf.Cos(Mathf.Lerp(-Mathf.Pi / 2, Mathf.Pi / 2, i / (sampleCount - 1f)))).ToArray();
        setting.IndependentRadius.CombineLatest(globalRadius, (own, global) => own ?? global)
            .CombineLatest(setting.Pressure2RadiusCurve, setting.ActiveBrushFlags, ValueTuple.Create)
            .Subscribe(values =>
            {
                var radius = MapPreviewRadius(values.Item1);
                var radii = pressures.Select(pressure => radius * (values.Item3.HasFlag(BrushFlags.Pressure2Radius)
                    ? values.Item2.SampleX(pressure) : 1)).ToArray();
                float margin = radii.Max() + 2f;
                float padding = Mathf.Max(16f, margin);
                float amplitude = Mathf.Min(24f, StrokeBufferSize.Y / 2f - margin);
                var positions = Enumerable.Range(0, sampleCount).Select(i => new Vector2(
                    Mathf.Lerp(padding, StrokeBufferSize.X - padding, i / (sampleCount - 1f)),
                    StrokeBufferSize.Y / 2f + amplitude * Mathf.Sin(-Mathf.Pi + 2 * Mathf.Pi * i / (sampleCount - 1f)))).ToArray();
                stroke.SetGeometry(positions, radii, pressures);
            }).AddTo(viewport);
        return viewport;
    }

    private static float MapPreviewRadius(float radius)
    {
        // Anchor the default brush to a readable radius, with smooth limits at both extremes.
        float relative = radius / BrushManager.DefaultStrokeRadius;
        float squared = relative * relative;
        float shoulder = (MaxPreviewRadius - DefaultPreviewRadius) / (DefaultPreviewRadius - MinPreviewRadius);
        return MinPreviewRadius + (MaxPreviewRadius - MinPreviewRadius) * squared / (squared + shoulder);
    }

    private static Control Stroke(StrokeBrushSetting setting, Texture2D texture)
    {
        var aspect = new AspectRatioContainer
        {
            Name = "StrokePreview",
            Ratio = (float)texture.GetWidth() / texture.GetHeight(),
            StretchMode = AspectRatioContainer.StretchModeEnum.Fit,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var wrapper = new PanelContainer()
        {
            Name = "Image",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        aspect.AddChild(wrapper);

        var checkerboard = new ColorRect
        {
            Material = AutoloadRendering.CheckerboardMaterial,
            Color = Colors.Transparent,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        wrapper.AddChild(checkerboard);

        var textureRect = new TextureRect
        {
            Name = "StrokeTexture",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            // The outer AspectRatioContainer owns the ratio; fitting again can expose fractional-pixel borders.
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = texture,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        wrapper.AddChild(textureRect);

        var nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
            ClipText = true,
        };
        nameLabel.AddThemeConstantOverride("outline_size", 8);
        var nameWrapper = new MarginContainer()
        {
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
        };
        nameWrapper.AddThemeConstantOverride("margin_bottom", 12);
        nameWrapper.AddThemeConstantOverride("margin_right", 8);
        nameWrapper.AddChild(nameLabel);
        wrapper.AddChild(nameWrapper);

        setting.Name.Subscribe(name => nameLabel.Text = name).AddTo(wrapper);


        return aspect;
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
            OffsetTop = 20,
            OffsetBottom = 20,
            GrowVertical = Control.GrowDirection.End,
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
        setting.FillColor.CombineLatest(setting.ActiveBrushFlags, e.Document.Get<ColorManager>().CurrentColor,
            (_, _, color) => setting.ResolveColor(color)).Subscribe(background.SetColor).AddTo(box);
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
