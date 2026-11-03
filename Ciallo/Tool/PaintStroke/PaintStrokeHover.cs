using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

[RegisterState]
public class PaintStrokeHover : Interaction, IPropertyProvider
{
    [StateAccess]
    public PaintStrokeTool Tool { get; set; }

    private MultiMeshInstance2D _snapDots;

    public override void Start(CursorButtonData data)
    {
        Document.Get<WorldBody>().DefaultCursorShape = Control.CursorShape.Cross;
        _snapDots = AutoloadRendering.CreateDots();
        Document.Get<WorldOverlay>().AddChild(_snapDots);
        RefreshSnap(data.WorldPosition);
    }
    public override void Moving(CursorMotionData data)
    {
        RefreshSnap(data.WorldPosition);
    }
    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel()
    {
        Document.Get<WorldBody>().DefaultCursorShape = default;
        _snapDots?.QueueFree();
        _snapDots = null;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        // ---- Mode selector
        var modeButton = new OptionButton
        {
            CustomMinimumSize = new(0, 32),
        };
        modeButton.AddItem("Freehand");
        modeButton.AddItem("Bezier");
        modeButton.AddItem("Poly Cubic Bézier");
        modeButton.BindSelectionIndex(Tool.Mode);
        container.AddProperty("Mode", modeButton);

        // ---- App brush library
        var brushSelector = new OptionButton()
        {
            CustomMinimumSize = new(256, 32),
            FitToLongestItem = false,
        }
            .ObserveObservableList(AppStrokeBrushLibrary.BrushSettings, s => s.Name)
            .BindSelectionIndex(AppStrokeBrushLibrary.SelectedIndex);
        container.AddProperty("Library brush", brushSelector);

        var radiusView = AppStrokeBrushLibrary.SelectedIndex
            .Select(idx => AppStrokeBrushLibrary.BrushSettings.ElementAtOrDefault(idx)?.BaseRadius)
            .Flatten();
        var appBrushRadiusControl = new SpinSlider()
        {
            MinValue = 0.1f,
            MaxValue = 256f,
            Step = 0.03333333f,
            ExpEdit = true
        }.BindNumber(radiusView);
        radiusView.AddTo(appBrushRadiusControl);

        container.AddProperty("Radius", appBrushRadiusControl)
            .VisibleIf(AppStrokeBrushLibrary.SelectedIndex, v => v >= 0);

        var colorView = AppStrokeBrushLibrary.SelectedIndex
            .Select(idx => AppStrokeBrushLibrary.BrushSettings.ElementAtOrDefault(idx)?.Color)
            .Flatten();
        var appBrushColorControl = new ColorPickerButton()
        {
            CustomMinimumSize = new(0, 32),
        }.BindColor(colorView);
        colorView.AddTo(appBrushColorControl);
        container.AddProperty("Color", appBrushColorControl)
            .VisibleIf(AppStrokeBrushLibrary.SelectedIndex, v => v >= 0);

        var useBrushButton = new Button()
        {
            Text = "Use brush",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new(0, 32),
            SizeFlagsHorizontal = Control.SizeFlags.Fill
        };
        useBrushButton.VisibleIf(AppStrokeBrushLibrary.SelectedIndex, v => v >= 0);
        useBrushButton.Pressed += OnUseBrushPressed;
        var manageButton = new Button()
        {
            Text = "Manage brush library",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new(0, 32),
            SizeFlagsHorizontal = Control.SizeFlags.Fill
        };

        manageButton.Pressed += () =>
        {
            AppDialogHost.BrushLibrary.Popup();
        };

        var box = new VBoxContainer()
        {
            SizeFlagsHorizontal = Control.SizeFlags.Fill
        };
        box.AddChild(manageButton);
        box.AddChild(useBrushButton);
        container.AddChild(box);
        // ---------------------------------------------
        container.AddChild(new HSeparator());
        // ---------------------------------------------
        // ---- Document brush library
        var brushPreview = StrokeBrushPreviewList.New(Document);
        brushPreview.CustomMinimumSize = new(0, 256);
        container.AddChild(brushPreview);

        var selectionM = Document.Get<SelectionManager>();
        var radius = selectionM.WorkingStrokeBrush
            .Select(e => e.IsDyingOrDead ? null : e.Get<StrokeBrushSetting>().BaseRadius)
            .Flatten();
        var radiusControl = new SpinSlider
        {
            MinValue = 0.01f,
            MaxValue = 256f,
            Step = 0.01f,
            AllowGreater = true,
            ExpEdit = true,
        }.BindNumber(radius);
        radius.AddTo(radiusControl);
        container.AddProperty("Radius", radiusControl)
            .VisibleIf(selectionM.WorkingStrokeBrush, Entity.IsNotNull);

    }

    private void OnUseBrushPressed()
    {
        if (!AppStrokeBrushLibrary.HasSelection) return;
        var setting = AppStrokeBrushLibrary.SelectedBrushSetting.CurrentValue;
        new CommandBuilder("Use Library Stroke Brush", Document.World.Create())
            .NewStrokeBrush(setting)
            .SetWorkingStrokeBrush()
            .Commit();
        AppStrokeBrushLibrary.SelectedIndex.Value = -1;
    }

    internal void RefreshSnapping() => RefreshSnap(LatestCursor.WorldPosition);

    private void RefreshSnap(Vector2 worldPosition)
    {
        var target = Tool.TryFindSnapTarget(worldPosition);
        if (!target.HasValue)
        {
            _snapDots.Visible = false;
            return;
        }

        _snapDots.Visible = true;
        _snapDots.SetDotGeometry([target.Value.HitPoint], AppPreference.StrokeDotRadius);
    }
}
