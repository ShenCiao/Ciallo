using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

[SceneTree, Instantiable]
public partial class StrokeBrushEditor : AcceptDialog
{
    private Entity _document;
    private StrokeBrushSetting _setting;
    private CommandManager _history;
    private bool _library;
    private Label _scope;
    private StrokeBrushDetailPreview _preview;
    private readonly StrokeBrushSetting _editable = new();
    private readonly ReactiveProperty<float> _previewRadius = new(BrushManager.DefaultStrokeRadius);
    private readonly CompositeDisposable _bindings = new();
    private bool _followWorking;

    public void Init(Entity document) { _document = document; _followWorking = !document.IsNull; }

    public void InitSetting(StrokeBrushSetting setting, Entity document, CommandManager history, bool library)
    {
        _setting = setting;
        _document = document;
        _followWorking = false;
        _history = history;
        _library = library;
    }

    public override void _Ready()
    {
        GetOkButton().Text = "Close";
        // The scrollable content must not resize the dialog while flow rows wrap.
        var content = new Control { Name = "Content", CustomMinimumSize = new(480, 360) };
        AddChild(content);
        var layout = new VBoxContainer { Name = "Layout" };
        _scope = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Size = new(624, 64),
        };
        layout.AddChild(_scope);
        _previewRadius.AddTo(this);
        _preview = new StrokeBrushDetailPreview(_editable, _previewRadius) { Name = "Preview" };
        layout.AddChild(_preview);
        var scroll = new ScrollContainer
        {
            Name = "PropertiesScroll",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Reserve,
        };
        var propertiesMargin = new MarginContainer
        {
            Name = "PropertiesMargin", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        propertiesMargin.AddThemeConstantOverride("margin_top", 12);
        layout.AddChild(propertiesMargin);
        propertiesMargin.AddChild(scroll);
        var properties = new PropertyContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(properties);
        _editable.DrawProperty(properties);
        content.AddChild(layout);
        layout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AboutToPopup += BindSetting;
        VisibilityChanged += () => { if (!Visible) _bindings.Clear(); };
    }

    private void BindSetting()
    {
        _bindings.Clear();
        _preview.ResetView();
        if (_followWorking)
        {
            var brush = _document.Get<SelectionManager>().WorkingStrokeBrush.Value;
            if (brush.IsNull) { Hide(); return; }
            _setting = brush.Get<StrokeBrushSetting>();
            _history = _document.Get<CommandManager>();
        }
        Title = _library ? "Library brush settings" : "Document brush settings";
        _scope.Text = _library ? "[Library Brushes Scope]" : "[Document Brushes Scope]";
        (_library ? Observable.Return(BrushManager.DefaultStrokeRadius)
            : _document.Get<BrushManager>().CurrentStrokeRadius.AsObservable())
            .Subscribe(radius => _previewRadius.Value = radius).AddTo(_bindings);
        Bind(_setting.Name, _editable.Name, "Name");
        Bind(_setting.Color, _editable.Color, "Color");
        Bind(_setting.ActiveBrushFlags, _editable.ActiveBrushFlags, "ActiveBrushFlags");
        Bind(_setting.BlendMode, _editable.BlendMode, "BlendMode");
        Bind(_setting.IndependentRadius, _editable.IndependentRadius, "IndependentRadius");
        Bind(_setting.Pressure2RadiusCurve, _editable.Pressure2RadiusCurve, "Pressure2RadiusCurve");
        Bind(_setting.RenderingType, _editable.RenderingType, "RenderingType");
        Bind(_setting.Pressure2FlowCurve, _editable.Pressure2FlowCurve, "Pressure2FlowCurve");
        Bind(_setting.DashLength, _editable.DashLength, "DashLength");
        Bind(_setting.GapLength, _editable.GapLength, "GapLength");
        Bind(_setting.DashForwardSpeed, _editable.DashForwardSpeed, "DashForwardSpeed");
        Bind(_setting.ActiveStampFlags, _editable.ActiveStampFlags, "ActiveStampFlags");
        Bind(_setting.StampInterval, _editable.StampInterval, "StampInterval");
        Bind(_setting.StampTexture, _editable.StampTexture, "StampTexture");
        Bind(_setting.DiskOpacityCurve, _editable.DiskOpacityCurve, "DiskOpacityCurve");
        Bind(_setting.StampRotation, _editable.StampRotation, "StampRotation");
        Bind(_setting.MaskTexture, _editable.MaskTexture, "MaskTexture");
        Bind(_setting.RotationNoiseAmplitude, _editable.RotationNoiseAmplitude, "RotationNoiseAmplitude");
        Bind(_setting.FalloffCurve, _editable.FalloffCurve, "FalloffCurve");
        Bind(_setting.AlphaDensity, _editable.AlphaDensity, "AlphaDensity");
        _history.HistoryNavigated.Subscribe(_ =>
        {
            bool exists = _library ? AppStrokeBrushLibrary.BrushSettings.Contains(_setting)
                : _document.Get<BrushManager>().StrokeBrushes.Any(e => ReferenceEquals(e.Get<StrokeBrushSetting>(), _setting));
            if (!exists) Hide();
        }).AddTo(_bindings);
    }

    private void Bind<T>(ReactiveProperty<T> property, ReactiveProperty<T> proxy, string name)
    {
        bool updating = false;
        long lastEditEpoch = -1;
        property.Subscribe(value =>
        {
            updating = true;
            proxy.Value = value;
            updating = false;
        }).AddTo(_bindings);
        proxy.Skip(1).Subscribe(value =>
        {
            if (updating) return;
            var command = new CommandBuilder("Edit Brush " + name).SetProperty(property, value);
            // Only compress this binding's consecutive edits, never another brush or a navigated history.
            if (lastEditEpoch == _history.PersistenceEpoch)
                _history.CommitSequence(command.ActionName, command.Commands);
            else
                _history.Commit(command.ActionName, command.Commands);
            lastEditEpoch = _history.PersistenceEpoch;
        }).AddTo(_bindings);
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        bool undo = AppHotkeys.Global.EditUndo.IsPressedBy(input);
        bool redo = AppHotkeys.Global.EditRedo.IsPressedBy(input);
        if (!undo && !redo) return;
        if (redo) _history.Redo(); else _history.Undo();
        SetInputAsHandled();
    }

    public override void _ExitTree() => _bindings.Dispose();
}
