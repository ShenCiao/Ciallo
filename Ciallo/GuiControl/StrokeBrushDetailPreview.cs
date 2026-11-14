using Ciallo.Data;
using Ciallo.Rendering;
using Godot;
using R3;

namespace Ciallo.GuiControl;

internal partial class StrokeBrushDetailPreview : PanelContainer
{
    private readonly SubViewport _viewport;
    private readonly Camera2D _camera;
    private bool _panning;
    private float _zoom = 1;

    public StrokeBrushDetailPreview(StrokeBrushSetting setting, Observable<float> radius)
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new(0, 160);
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        AddChild(new ColorRect
        {
            Material = AutoloadRendering.CheckerboardMaterial,
            Color = Colors.Transparent,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        var display = new SubViewportContainer
        {
            Name = "Display", Stretch = true, StretchShrink = 1,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(display);
        var material = new StrokeBrushMaterial();
        material.ObserveBrushSetting(setting);
        material.Subs.AddTo(this);
        var color = AppDocumentManager.WorkingDocument.Select(document => document.IsNull
            ? Observable.Return(Colors.Black) : document.Get<ColorManager>().CurrentColor.AsObservable()).Switch();
        // A separate render target follows the display size; only geometry/material logic is shared with thumbnails.
        _viewport = BrushPreview.CreateStrokeViewport(setting, material, color, radius, sampleCount: 128);
        _viewport.Name = "PreviewViewport";
        display.AddChild(_viewport);
        _camera = new Camera2D { Name = "Camera", Position = (Vector2)BrushPreview.StrokeBufferSize / 2 };
        _viewport.AddChild(_camera);
        _viewport.SizeChanged += UpdateCamera;
        Resized += () => CustomMinimumSize = new(0, Mathf.Clamp(Size.X / BrushPreview.StrokeAspectRatio, 160, 280));
        VisibilityChanged += () => _panning = false;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        var fit = (Vector2)_viewport.Size / BrushPreview.StrokeBufferSize;
        _camera.Zoom = Vector2.One * (Mathf.Min(fit.X, fit.Y) * _zoom);
    }

    public void ResetView()
    {
        _panning = false;
        _zoom = 1;
        _camera.Offset = Vector2.Zero;
        UpdateCamera();
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle)
        {
            _panning = middle.Pressed;
            if (middle.DoubleClick) ResetView();
            AcceptEvent();
        }
        else if (input is InputEventMouseMotion motion && _panning)
        {
            _camera.Offset -= motion.Relative / _camera.Zoom;
            AcceptEvent();
        }
        else if (input is InputEventMouseButton { Pressed: true } wheel &&
                 wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            float factor = 1 + AppPreference.MouseWheelZoomFactor.Value;
            _zoom = Mathf.Clamp(_zoom * (wheel.ButtonIndex == MouseButton.WheelUp ? factor : 1 / factor), 0.25f, 16f);
            UpdateCamera();
            AcceptEvent();
        }
    }
}
