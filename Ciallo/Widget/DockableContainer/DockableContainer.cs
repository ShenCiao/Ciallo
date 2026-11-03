using System.Collections.Generic;
using Godot;

namespace Ciallo.Widget;

[Tool, GlobalClass, Icon("res://addons/dockable_container/icon.svg")]
public partial class DockableContainer : Container, ISerializationListener
{
    public const string TitleMetadata = "dockable_title";
    public const string ExclusiveMetadata = "dockable_exclusive";

    [Signal]
    public delegate void LayoutChangedEventHandler();

    private Container _panelContainer;
    private Container _splitContainer;
    private DockableDragNDropPanel _dragNDropPanel;
    private readonly Dictionary<Node, string> _nameByChild = new();
    private readonly Dictionary<string, Node> _childByName = new();

    private DockablePanel _dragPanel;
    private int _currentPanelIndex;
    private int _currentSplitIndex;
    private Vector2 _layoutMinimumSize;

    [Export]
    public TabBar.AlignmentMode TabAlignment
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            foreach (var panel in GetPanels())
                panel.TabAlignment = value;
        }
    } = TabBar.AlignmentMode.Left;

    [Export]
    public bool UseHiddenTabsForMinSize
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            foreach (var panel in GetPanels())
                panel.UseHiddenTabsForMinSize = value;
        }
    }

    [Export]
    public bool TabsVisible
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            foreach (var panel in GetPanels())
                panel.ShowTabs = value;
        }
    } = true;

    [Export]
    public bool HideSingleTab
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            foreach (var panel in GetPanels())
                panel.HideSingleTab = value;
        }
    }

    [Export]
    public int RearrangeGroup
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            foreach (var panel in GetPanels())
                panel.SetTabsRearrangeGroup(panel.HideTabs ? -1 : Mathf.Max(0, value));
        }
    }

    [Export]
    public DockableLayout Layout
    {
        get;
        set
        {
            value ??= new DockableLayout();
            var layoutChanged = new Callable(this, MethodName.OnLayoutResourceChanged);
            DockableLayout previousLayout = field;
            // Tool-script reloads may restore connections to superseded callback names.
            DisconnectLegacyLayoutSignal(previousLayout);
            DisconnectLegacyLayoutSignal(value);
            if (field == value)
            {
                DockableSignalConnection.EnsureConnected(value, Resource.SignalName.Changed, layoutChanged);
                return;
            }

            field = value;
            DockableSignalConnection.Rebind(previousLayout, value, Resource.SignalName.Changed, layoutChanged);
            QueueSort();
        }
    }

    [Export]
    public bool CloneLayoutOnReady { get; set; } = true;

    public DockableContainer()
    {
        // Godot can retain native connections while replacing a C# tool-script instance.
        DisconnectLegacyChildSignals();
        Layout = new DockableLayout();
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        EnsureInternalNodes();
        ClipContents = true;
        DisconnectLegacyChildSignals();
        DisconnectLegacyLayoutSignal(Layout);
        DockableSignalConnection.EnsureConnected(
            Layout,
            Resource.SignalName.Changed,
            new Callable(this, MethodName.OnLayoutResourceChanged)
        );

        if (IsInsideTree())
            BindSceneTreeSignals();

        QueueSort();
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        BindSceneTreeSignals();
    }

    public override void _Ready()
    {
        base._Ready();
        SetProcessInput(false);

        EnsureInternalNodes();
        ClipContents = true;

        if (CloneLayoutOnReady && !Engine.IsEditorHint())
            Layout = Layout.Clone();
    }

    public override Vector2 _GetMinimumSize() => _layoutMinimumSize;

    public override void _ExitTree()
    {
        UnbindSceneTreeSignals();
        base._ExitTree();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        if (what == NotificationChildOrderChanged)
        {
            QueueSort();
        }
        else if (what == NotificationSortChildren)
        {
            Resort();
        }
        else if (what == NotificationTranslationChanged)
        {
            RefreshTabTitles();
            QueueSort();
        }
        else if (what == NotificationThemeChanged)
        {
            QueueSort();
        }
        else if (what == NotificationDragBegin && CanHandleDragData(GetViewport().GuiGetDragData()))
        {
            _dragNDropPanel.SetEnabled(true, !Layout.Root.IsEmpty());
            SetProcessInput(true);
        }
        else if (what == NotificationDragEnd)
        {
            _dragNDropPanel.SetEnabled(false);
            SetProcessInput(false);
        }
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event is not InputEventMouseMotion) return;

        // Viewport input keeps the preview tracking across panel boundaries during a tab drag.
        Vector2 localPosition = GetLocalMousePosition();
        DockablePanel panel = null;
        foreach (var candidate in GetPanels())
        {
            if (!candidate.GetRect().HasPoint(localPosition)) continue;
            panel = candidate;
            break;
        }

        _dragPanel = panel;
        if (panel == null) return;
        FitChildInRect(_dragNDropPanel, panel.GetChildRect());
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => CanHandleDragData(data);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var dictionary = data.AsGodotDictionary();
        var fromPath = dictionary["from_path"].AsNodePath();
        var fromNode = NormalizeDragSource(GetNode(fromPath));

        if (fromNode == _dragPanel && _dragPanel.GetChildCount() == 1)
            return;

        int tabIndex = dictionary.ContainsKey("tabc_element")
            ? dictionary["tabc_element"].AsInt32()
            : dictionary["tab_index"].AsInt32();
        var movedTab = ((TabContainer)fromNode).GetTabControl(tabIndex);
        // DockablePanel hosts proxies; ownership and layout identity stay with the referenced control.
        if (movedTab is DockableReferenceControl referenceControl)
            movedTab = referenceControl.ReferenceTo;

        if (!IsManagedNode(movedTab))
        {
            movedTab.GetParent().RemoveChild(movedTab);
            AddChild(movedTab);
        }

        if (_dragPanel != null)
        {
            int margin = _dragNDropPanel.GetHoverMargin();
            Layout.SplitLeafWithNode(_dragPanel.Leaf, movedTab, margin);
        }

        QueueSort();
    }

    public void SetControlAsCurrentTab(Control control)
    {
        if (control.GetParentControl() != this)
            throw new System.InvalidOperationException("Trying to focus a control not managed by this container");
        if (IsControlHidden(control))
        {
            GD.PushWarning("Trying to focus a hidden control");
            return;
        }

        var leaf = Layout.GetLeafForNode(control);
        if (leaf == null) return;

        int positionInLeaf = leaf.FindChild(control);
        if (positionInLeaf < 0) return;

        leaf.CurrentTab = positionInLeaf;
        QueueSort();
    }

    public void SetLayout(DockableLayout value) => Layout = value;

    public void SetControlHidden(Control child, bool isHidden)
    {
        if (isHidden && IsExclusive(child))
            throw new System.InvalidOperationException($"Exclusive control '{child.Name}' cannot be hidden");
        Layout.SetNodeHidden(child, isHidden);
    }

    public bool IsControlHidden(Control child) => Layout.IsNodeHidden(child);

    public Control[] GetTabs()
    {
        var tabs = new List<Control>();
        foreach (Node child in GetChildren())
        {
            if (IsManagedNode(child))
                tabs.Add((Control)child);
        }
        return tabs.ToArray();
    }

    public int GetTabCount()
    {
        int count = 0;
        foreach (Node child in GetChildren())
        {
            if (IsManagedNode(child))
                count++;
        }
        return count;
    }

    public bool IsLayoutValid(DockableLayout layout)
    {
        if (layout == null) return false;

        var managedNames = new HashSet<string>();
        var exclusiveNames = new HashSet<string>();
        foreach (Node child in GetChildren())
        {
            if (!IsManagedNode(child)) continue;
            string name = child.Name;
            managedNames.Add(name);
            if (IsExclusive(child))
                exclusiveNames.Add(name);
        }

        var layoutNames = new HashSet<string>();
        if (!ValidateLayoutNode(layout.Root, managedNames, exclusiveNames, layoutNames))
            return false;
        if (!layoutNames.SetEquals(managedNames))
            return false;

        if (layout.HiddenTabs == null)
            return false;
        foreach (Variant hiddenName in layout.HiddenTabs.Keys)
        {
            if (hiddenName.VariantType != Variant.Type.String)
                return false;
            string name = hiddenName.AsString();
            if (!managedNames.Contains(name) || exclusiveNames.Contains(name))
                return false;
            Variant hiddenValue = layout.HiddenTabs[hiddenName];
            if (hiddenValue.VariantType != Variant.Type.Bool || !hiddenValue.AsBool())
                return false;
        }

        return true;
    }

    private bool CanHandleDragData(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return false;

        // TabBar and TabContainer emit equivalent drag payloads under different keys.
        var dictionary = data.AsGodotDictionary();
        string type = dictionary.TryGetValue("type", out Variant typeValue) ? typeValue.AsString() : "";
        string tabType = dictionary.TryGetValue("tab_type", out Variant tabTypeValue) ? tabTypeValue.AsString() : "";
        bool isValidType = type is "tab" or "tab_container_tab" or "tabc_element"
            || tabType is "tab_container_tab" or "tabc_element";
        if (!isValidType || !dictionary.TryGetValue("from_path", out Variant fromPathValue)) return false;

        var sourceNode = NormalizeDragSource(GetNodeOrNull(fromPathValue.AsNodePath()));
        if (sourceNode == null || !sourceNode.HasMethod("get_tabs_rearrange_group")) return false;
        Variant result = sourceNode.Call("get_tabs_rearrange_group");
        return result.AsInt32() == RearrangeGroup;
    }

    private bool IsManagedNode(Node node) =>
        node.GetParent() == this
        && node != _panelContainer
        && node != _dragNDropPanel
        && node is Control control
        && !control.TopLevel;

    private static bool IsExclusive(Node node) =>
        node.HasMeta(ExclusiveMetadata) && node.GetMeta(ExclusiveMetadata).AsBool();

    private static Node NormalizeDragSource(Node node) =>
        node is TabBar ? node.GetParent() : node;

    private void EnsureInternalNodes()
    {
        // Reuse native helper nodes restored across tool-script reloads before creating any replacements.
        RebindInternalNodes();

        _panelContainer ??= new Container();
        _panelContainer.Name = "_panel_container";
        if (_panelContainer.GetParent() == null)
            AddChild(_panelContainer, false, Node.InternalMode.Front);

        _splitContainer ??= new Container();
        _splitContainer.Name = "_split_container";
        _splitContainer.MouseFilter = MouseFilterEnum.Pass;
        if (_splitContainer.GetParent() == null)
            _panelContainer.AddChild(_splitContainer, false, Node.InternalMode.Front);

        _dragNDropPanel ??= new DockableDragNDropPanel();
        _dragNDropPanel.Name = "_drag_n_drop_panel";
        _dragNDropPanel.MouseFilter = MouseFilterEnum.Pass;
        _dragNDropPanel.Visible = false;
        if (_dragNDropPanel.GetParent() == null)
            AddChild(_dragNDropPanel, false, Node.InternalMode.Back);
    }

    private void RebindInternalNodes()
    {
        var panelContainer = GetNodeOrNull<Container>("_panel_container");
        if (panelContainer != null)
            _panelContainer = panelContainer;

        if (_panelContainer != null)
        {
            var splitContainer = _panelContainer.GetNodeOrNull<Container>("_split_container");
            if (splitContainer != null)
                _splitContainer = splitContainer;
        }

        var dragNDropPanel = GetNodeOrNull<DockableDragNDropPanel>("_drag_n_drop_panel");
        if (dragNDropPanel != null)
            _dragNDropPanel = dragNDropPanel;
    }

    private void BindSceneTreeSignals()
    {
        DockableSignalConnection.EnsureConnected(
            GetTree(),
            SceneTree.SignalName.NodeRenamed,
            new Callable(this, MethodName.OnSceneTreeNodeRenamed)
        );
    }

    private void UnbindSceneTreeSignals()
    {
        if (!IsInsideTree()) return;
        DockableSignalConnection.Disconnect(
            GetTree(),
            SceneTree.SignalName.NodeRenamed,
            new Callable(this, MethodName.OnSceneTreeNodeRenamed)
        );
    }

    private void DisconnectLegacyChildSignals()
    {
        DockableSignalConnection.Disconnect(
            this,
            Node.SignalName.ChildEnteredTree,
            new Callable(this, "OnContainerChildEnteredTree")
        );
        DockableSignalConnection.Disconnect(
            this,
            Node.SignalName.ChildExitingTree,
            new Callable(this, "OnContainerChildExitingTree")
        );
    }

    private void DisconnectLegacyLayoutSignal(DockableLayout layout)
    {
        DockableSignalConnection.Disconnect(
            layout,
            Resource.SignalName.Changed,
            new Callable(this, "OnLayoutChanged")
        );
    }

    private void ReconcileChildren()
    {
        var names = new List<string>();
        // This snapshot catches renames made while the container was outside the scene tree.
        var previousNameByChild = new Dictionary<Node, string>(_nameByChild);
        _nameByChild.Clear();
        _childByName.Clear();

        foreach (Node child in GetChildren())
        {
            if (!IsManagedNode(child)) continue;

            var legacyRenamed = new Callable(this, "OnTrackedChildRenamed");
            DockableSignalConnection.Disconnect(child, Node.SignalName.Renamed, legacyRenamed);
            if (previousNameByChild.TryGetValue(child, out string previousName) && previousName != child.Name)
                Layout.RenameNode(previousName, child.Name);
            _nameByChild[child] = child.Name;
            _childByName[child.Name] = child;
            names.Add(child.Name);
        }

        Layout.UpdateNodes(names);
    }

    private void Resort()
    {
        ReconcileChildren();

        // Helper controls are a recycled projection; Layout remains the authoritative tree.
        _currentPanelIndex = 0;
        _currentSplitIndex = 0;

        var childrenList = new List<Control>();
        Control layoutRoot = CalculatePanelAndSplitList(childrenList, Layout.Root);
        SetLayoutMinimumSize(layoutRoot == null ? Vector2.Zero : GetLayoutMinimumSize(layoutRoot));

        // Minimum-size propagation can trail this layout pass, so never fit children into less space.
        var rect = new Rect2(Vector2.Zero, Size.Max(GetCombinedMinimumSize()));
        FitChildInRectIfChanged(this, _panelContainer, rect);
        FitChildInRectIfChanged(_panelContainer, _splitContainer, rect);
        FitPanelAndSplitListToRect(childrenList, rect);

        UntrackChildrenAfter(_panelContainer, _currentPanelIndex);
        UntrackChildrenAfter(_splitContainer, _currentSplitIndex);
    }

    private Control CalculatePanelAndSplitList(List<Control> result, DockableLayoutNode layoutNode)
    {
        switch (layoutNode)
        {
            case DockableLayoutPanel layoutPanel:
                {
                    var nodes = new List<Control>();
                    bool exclusive = false;
                    foreach (string name in layoutPanel.Names)
                    {
                        if (!_childByName.TryGetValue(name, out var child)) continue;
                        var node = (Control)child;
                        if (IsExclusive(node))
                        {
                            if (layoutPanel.Names.Length != 1)
                                throw new System.InvalidOperationException($"Exclusive control '{name}' must occupy its own layout leaf");
                            if (Layout.HiddenTabs.ContainsKey(name))
                                throw new System.InvalidOperationException($"Exclusive control '{name}' cannot be hidden");
                            exclusive = true;
                        }
                        if (IsControlHidden(node))
                            SetVisibleIfChanged(node, false);
                        else
                            nodes.Add(node);
                    }

                    if (nodes.Count == 0) return null;

                    var panel = GetPanel(_currentPanelIndex);
                    _currentPanelIndex++;
                    var titles = new string[nodes.Count];
                    for (int i = 0; i < nodes.Count; i++)
                        titles[i] = GetControlTitle(nodes[i]);
                    panel.TrackNodes(nodes.ToArray(), titles, layoutPanel, exclusive);
                    panel.SetTabsRearrangeGroup(exclusive ? -1 : Mathf.Max(0, RearrangeGroup));
                    result.Add(panel);
                    return panel;
                }
            case DockableLayoutSplit layoutSplit:
                {
                    // Fitting consumes this reverse-postorder list from the end: split, first, second.
                    var secondResult = CalculatePanelAndSplitList(result, layoutSplit.Second);
                    var firstResult = CalculatePanelAndSplitList(result, layoutSplit.First);

                    if (firstResult != null && secondResult != null)
                    {
                        var split = GetSplit(_currentSplitIndex);
                        _currentSplitIndex++;
                        split.LayoutSplit = layoutSplit;
                        split.FirstMinimumSize = GetLayoutMinimumSize(firstResult);
                        split.SecondMinimumSize = GetLayoutMinimumSize(secondResult);
                        result.Add(split);
                        return split;
                    }

                    return firstResult ?? secondResult;
                }
            default:
                throw new System.InvalidOperationException($"Invalid Resource, should be branch or leaf, found {layoutNode}");
        }
    }

    private void FitPanelAndSplitListToRect(List<Control> panelAndSplitList, Rect2 rect)
    {
        if (panelAndSplitList.Count == 0) return;

        var control = panelAndSplitList[^1];
        panelAndSplitList.RemoveAt(panelAndSplitList.Count - 1);

        if (control is DockablePanel panel)
        {
            FitChildInRectIfChanged(_panelContainer, panel, rect);
        }
        else if (control is DockableSplitHandle split)
        {
            var splitRects = split.GetSplitRects(rect);
            FitChildInRectIfChanged(_splitContainer, split, splitRects.Self);
            FitPanelAndSplitListToRect(panelAndSplitList, splitRects.First);
            FitPanelAndSplitListToRect(panelAndSplitList, splitRects.Second);
        }
    }

    private DockablePanel GetPanel(int index)
    {
        if (index < _panelContainer.GetChildCount())
            return (DockablePanel)_panelContainer.GetChild(index);

        var panel = new DockablePanel
        {
            TabAlignment = TabAlignment,
            ShowTabs = TabsVisible,
            HideSingleTab = HideSingleTab,
            UseHiddenTabsForMinSize = UseHiddenTabsForMinSize,
        };
        panel.SetTabsRearrangeGroup(Mathf.Max(0, RearrangeGroup));
        // Capturing panel in a lambda can reload as: "Can't get method on CallableCustom 'Delegate::Invoke'".
        panel.Connect(DockablePanel.SignalName.TabLayoutChanged, new Callable(this, MethodName.OnPanelTabLayoutChanged));
        _panelContainer.AddChild(panel);
        return panel;
    }

    private DockableSplitHandle GetSplit(int index)
    {
        if (index < _splitContainer.GetChildCount())
            return (DockableSplitHandle)_splitContainer.GetChild(index);

        var split = new DockableSplitHandle();
        _splitContainer.AddChild(split);
        return split;
    }

    private void UntrackChildrenAfter(Control node, int index)
    {
        while (node.GetChildCount() > index)
        {
            var child = node.GetChild(index);
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnPanelTabLayoutChanged(int tab, DockablePanel panel)
    {
        var control = panel.GetTabControl(tab);
        if (control is DockableReferenceControl referenceControl)
            control = referenceControl.ReferenceTo;

        if (IsExclusive(control) || LeafContainsExclusive(panel.Leaf))
            throw new System.InvalidOperationException("Exclusive controls cannot be combined with other controls in a layout leaf");

        if (!IsManagedNode(control))
        {
            control.GetParent().RemoveChild(control);
            AddChild(control);
        }

        int rawPosition = GetRawTabInsertionIndex(panel.Leaf, control.Name, tab);
        Layout.MoveNodeToLeaf(control, panel.Leaf, rawPosition);
        panel.Leaf.CurrentTab = rawPosition;
        QueueSort();
    }

    private int GetRawTabInsertionIndex(DockableLayoutPanel leaf, string movingName, int visiblePosition)
    {
        // TabContainer reports visible positions; the resource order also contains hidden tabs.
        int rawPosition = 0;
        int currentVisiblePosition = 0;
        foreach (string name in leaf.Names)
        {
            if (name == movingName) continue;
            if (!Layout.IsTabHidden(name))
            {
                if (currentVisiblePosition == visiblePosition)
                    return rawPosition;
                currentVisiblePosition++;
            }
            rawPosition++;
        }

        return rawPosition;
    }

    private void OnLayoutResourceChanged()
    {
        QueueSort();
        EmitSignal(SignalName.LayoutChanged);
    }

    private void OnSceneTreeNodeRenamed(Node child)
    {
        if (child.GetParent() != this || !_nameByChild.TryGetValue(child, out string oldName))
            return;
        if (oldName == child.Name) return;

        _childByName.Remove(oldName);
        _nameByChild[child] = child.Name;
        _childByName[child.Name] = child;
        Layout.RenameNode(oldName, child.Name);
    }

    private IEnumerable<DockablePanel> GetPanels()
    {
        if (_panelContainer == null) yield break;
        for (int i = 0; i < _panelContainer.GetChildCount(); i++)
            yield return (DockablePanel)_panelContainer.GetChild(i);
    }

    private string GetControlTitle(Control control)
    {
        string title = control.HasMeta(TitleMetadata)
            ? control.GetMeta(TitleMetadata).AsString()
            : control.Name;
        return title;
    }

    private void RefreshTabTitles()
    {
        foreach (var panel in GetPanels())
        {
            for (int i = 0; i < panel.GetTabCount(); i++)
            {
                var reference = (DockableReferenceControl)panel.GetTabControl(i);
                string title = GetControlTitle(reference.ReferenceTo);
                if (panel.GetTabTitle(i) != title)
                    panel.SetTabTitle(i, title);
            }
        }
    }

    private bool LeafContainsExclusive(DockableLayoutPanel leaf)
    {
        foreach (string name in leaf.Names)
        {
            if (_childByName.TryGetValue(name, out var child) && IsExclusive(child))
                return true;
        }
        return false;
    }

    private static bool ValidateLayoutNode(
        DockableLayoutNode node,
        HashSet<string> managedNames,
        HashSet<string> exclusiveNames,
        HashSet<string> layoutNames)
    {
        if (managedNames.Count == 0)
            return node is DockableLayoutPanel emptyPanel
                && emptyPanel.Names != null
                && emptyPanel.Names.Length == 0;

        // A valid full binary layout with n managed controls cannot exceed 2n - 1 nodes.
        int maxNodeCount = managedNames.Count * 2 - 1;
        var pending = new Stack<(DockableLayoutNode Node, DockableLayoutSplit ExpectedParent)>();
        var visited = new HashSet<ulong>();
        pending.Push((node, null));

        while (pending.Count > 0)
        {
            (DockableLayoutNode current, DockableLayoutSplit expectedParent) = pending.Pop();
            if (current == null
                || current.Parent != expectedParent
                || !visited.Add(current.GetInstanceId())
                || visited.Count > maxNodeCount)
                return false;

            switch (current)
            {
                case DockableLayoutPanel panel:
                    if (panel.Names == null || panel.Names.Length == 0)
                        return false;

                    foreach (string name in panel.Names)
                    {
                        if (!managedNames.Contains(name) || !layoutNames.Add(name))
                            return false;
                        if (exclusiveNames.Contains(name) && panel.Names.Length != 1)
                            return false;
                    }
                    break;
                case DockableLayoutSplit split:
                    if (split.HasInvalidChildReference || split.HasInvalidPercent)
                        return false;
                    if (split.Direction != DockableLayoutSplit.SplitDirection.Horizontal
                        && split.Direction != DockableLayoutSplit.SplitDirection.Vertical)
                        return false;
                    if (!float.IsFinite(split.Percent) || split.Percent < 0 || split.Percent > 1)
                        return false;

                    pending.Push((split.Second, split));
                    pending.Push((split.First, split));
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static Vector2 GetLayoutMinimumSize(Control control) =>
        control switch
        {
            DockablePanel panel => panel.GetLayoutMinimumSize(),
            DockableSplitHandle split => split.GetLayoutMinimumSize(),
            _ => control.GetCombinedMinimumSize(),
        };

    private void SetLayoutMinimumSize(Vector2 value)
    {
        if (_layoutMinimumSize.IsEqualApprox(value)) return;
        _layoutMinimumSize = value;
        UpdateMinimumSize();
        OnLayoutMinimumSizeChanged();
    }

    protected virtual void OnLayoutMinimumSizeChanged()
    {
    }

    private static void FitChildInRectIfChanged(Container parent, Control child, Rect2 rect)
    {
        if (child.Position.IsEqualApprox(rect.Position) && child.Size.IsEqualApprox(rect.Size))
            return;
        parent.FitChildInRect(child, rect);
    }

    private static void SetVisibleIfChanged(CanvasItem item, bool visible)
    {
        if (item.Visible == visible) return;
        item.Visible = visible;
    }
}
