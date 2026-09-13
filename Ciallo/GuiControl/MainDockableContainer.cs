using Ciallo.Widget;
using Godot;

namespace Ciallo.GuiControl;

[Tool]
public partial class MainDockableContainer : DockableContainer
{
    public const string ScenePath = "MainGui/MarginContainer/DockableContainer";
    public const string ToolPanelName = "ToolPanel";
    public const string ToolPropertyPanelName = "ToolPropertyPanel";
    public const string LayerPanelName = "LayerPanel";
    public const string TimelinePanelName = "TimelinePanel";

    private const string UserLayoutPath = "user://DockableLayout.tres";
    private const double SaveDebounceSeconds = 0.75;

    private DockableLayout _defaultLayout;
    private Timer _layoutSaveTimer;

    public override void _Ready()
    {
        base._Ready();
        if (Engine.IsEditorHint()) return;

        // Preserve a pristine reset source before loading or mutating the user's layout.
        _defaultLayout = Layout.Clone();
        _layoutSaveTimer = new Timer
        {
            Name = "_layout_save_timer",
            OneShot = true,
            WaitTime = SaveDebounceSeconds,
        };
        _layoutSaveTimer.Connect(Timer.SignalName.Timeout, new Callable(this, MethodName.OnLayoutSaveTimerTimeout));
        AddChild(_layoutSaveTimer, false, Node.InternalMode.Back);

        LoadUserLayout();
        Connect(SignalName.LayoutChanged, new Callable(this, MethodName.OnLayoutChanged));
        CallDeferred(MethodName.UpdateWindowMinimumSize);
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationResized && !Engine.IsEditorHint() && IsNodeReady())
            CallDeferred(MethodName.UpdateWindowMinimumSize);
    }

    public bool IsAuxiliaryPanelVisible(string panelName) =>
        !IsControlHidden(GetNode<Control>(panelName));

    public void SetAuxiliaryPanelVisible(string panelName, bool visible)
    {
        var panel = GetNode<Control>(panelName);
        SetControlHidden(panel, !visible);
        if (visible)
            SetControlAsCurrentTab(panel);
    }

    public void ResetLayout()
    {
        _layoutSaveTimer.Stop();
        SetLayout(_defaultLayout.Clone());
        SaveLayout();
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint()) return;
        _layoutSaveTimer.Stop();
        SaveLayout();
    }

    private void LoadUserLayout()
    {
        if (AppCommandLineOptions.FactoryStartup || !FileAccess.FileExists(UserLayoutPath))
        {
            SetLayout(_defaultLayout.Clone());
            return;
        }

        // The layout is mutable; bypass both the resource and subresource caches on every startup.
        var resource = ResourceLoader.Load(UserLayoutPath, "", ResourceLoader.CacheMode.IgnoreDeep);
        if (resource is not DockableLayout layout)
        {
            GD.PushError($"Cannot load dock layout '{UserLayoutPath}': resource is not a DockableLayout.");
            SetLayout(_defaultLayout.Clone());
            return;
        }

        if (!IsLayoutValid(layout))
        {
            GD.PushError($"Cannot load dock layout '{UserLayoutPath}': layout is invalid.");
            SetLayout(_defaultLayout.Clone());
            return;
        }

        SetLayout(layout.Clone());
    }

    private void SaveLayout()
    {
        if (AppCommandLineOptions.FactoryStartup) return;
        var error = ResourceSaver.Save(Layout, UserLayoutPath);
        if (error != Error.Ok)
            GD.PushError($"Cannot save dock layout '{UserLayoutPath}': {error}.");
    }

    private void OnLayoutChanged() => _layoutSaveTimer.Start();

    private void OnLayoutSaveTimerTimeout() => SaveLayout();

    protected override void OnLayoutMinimumSizeChanged()
    {
        if (!Engine.IsEditorHint())
            CallDeferred(MethodName.UpdateWindowMinimumSize);
    }

    private void UpdateWindowMinimumSize()
    {
        var mainGui = GetParent<Control>().GetParent<Control>();
        // Convert the logical Control minimum to the window size at the active content scale.
        Vector2 minimumSize = mainGui.GetCombinedMinimumSize() * GetWindow().ContentScaleFactor;
        var windowMinimumSize = new Vector2I(
            Mathf.CeilToInt(minimumSize.X),
            Mathf.CeilToInt(minimumSize.Y)
        );

        if (GetWindow().MinSize != windowMinimumSize)
            GetWindow().MinSize = windowMinimumSize;
    }
}
