using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Misc;
using Ciallo.Widget;
using Godot;
using Massive;
using R3;

/// <summary>
/// Manage the layer UI controls. Also hold layer properties.
/// One instance per document.
/// </summary>
public partial class LayerContainer : Container
{
    public static readonly PackedScene LayerScene = GD.Load<PackedScene>("res://NodeControl/Layer.tscn");
    
    private VBoxContainer _rootControl; // all layers controls are direct children of this container, in preorder.
    private readonly ButtonGroup _workingLayerButtonGroup = new();

    private bool _isDragging = false;
    private Control _visibleDragHint;
    private Control _mouseHoveringLayer;
    
    private readonly Dictionary<Control, CompositeDisposable> _subscriptions = [];
    
    [OnInstantiate]
    private void Initialise()
    {
        
    }

    public override void _Ready()
    {
        _rootControl = GetNode<VBoxContainer>("%TreeRoot");
        // Free previews in the Godot editor.
        foreach (var child in _rootControl.GetChildren())
        {
            child.QueueFree();
        }
        _workingLayerButtonGroup.Pressed += button =>
        {
            var layerControl = (Control)button.GetOwner();
            new ChangeWorkingLayerCmd(layerControl.GetIndex()).Commit();
        };
    }

    public void CreateInsert(Entity layerE, int index)
    {
        var control = CreateAdd(layerE);
        _rootControl.MoveChild(control, index);
    }

    public Control CreateAdd(Entity layerE)
    {
        var layerControl = Create(layerE);
        _rootControl.AddChild(layerControl);
        layerE.Set(layerControl);
        return layerControl;
    }

    private Control Create(Entity e)
    {
        var node = e.Get<LayerTreeNode>();
        var layerControl = LayerScene.Instantiate<Container>();
        var subs = new CompositeDisposable();
        _subscriptions[layerControl] = subs;
        
        var activeButton = layerControl.GetNode<CheckBox>("%Active");
        activeButton.ButtonGroup = _workingLayerButtonGroup;
        
        var visibleButton = layerControl.GetNode<CheckBox>("%Visible");
        visibleButton.BindBool(node.IsVisible).AddTo(subs);

        var lineEdit = layerControl.GetNode<LabelLineEdit>("%LabelLineEdit");
        lineEdit.BindString(node.Name);
        
        layerControl.MouseEntered += () => _mouseHoveringLayer = layerControl;
        layerControl.MouseExited += () => _mouseHoveringLayer = null;
        
        var guiInput = lineEdit
            .SignalAsObservable<InputEvent>(Control.SignalName.GuiInput)
            .Where(_=>!lineEdit.IsEditing());
        var leftMouse = guiInput
            .OfType<InputEvent, InputEventMouseButton>()
            .Where(button => button.ButtonIndex == MouseButton.Left);
        
        // Single click without drag and double click
        var singleClickObs = leftMouse
            .Where(button => button.IsPressed() || button.IsReleased())
            .Chunk(TimeSpan.FromMilliseconds(200))
            .Where(xs => xs.Length == 2 && xs.First().IsPressed() && xs.Last().IsReleased())
            .Select(xs => xs.First());
        singleClickObs.Subscribe(_ => activeButton.SetPressed(true)).AddTo(subs);
        
        // Drag
        var mouseState = leftMouse.ToReadOnlyReactiveProperty();
        var dragStart = guiInput
            // The most recent left mouse is clicked and not release.
            .Where(_ => mouseState.CurrentValue?.IsPressed() == true)
            .OfType<InputEvent, InputEventMouseMotion>()
            .Where(motion => motion.ButtonMask == MouseButtonMask.Left)
            // mouse motion distance is larger than the value in pixels.
            .Where(motion => motion.GlobalPosition.DistanceTo(mouseState.CurrentValue.GlobalPosition) > 20)
            .Where(_ => !_isDragging);
        dragStart.Subscribe(motion =>
        {
            _isDragging = true;
            OnDragStart(layerControl, motion);
        }).AddTo(subs);

        var dragging = guiInput
            .Where(_ => _isDragging)
            .OfType<InputEvent, InputEventMouseMotion>()
            .Where(motion => motion.ButtonMask == MouseButtonMask.Left);
        dragging.Subscribe(motion => OnDragging(layerControl, motion)).AddTo(subs);

        var dragEnd = leftMouse
            .Where(button => _isDragging && button.IsReleased());
        dragEnd.Subscribe(button =>
        {
            _isDragging = false;
            OnDragEnd(layerControl, button);
        }).AddTo(subs);
        
        return layerControl;
    }

    private void Insert(int index, Control layerControl)
    {
        if (!_subscriptions.ContainsKey(layerControl))
            throw new ArgumentException("The given layer control is not created by this LayerTreeControl.");
        
        _rootControl.AddChild(layerControl);
        _rootControl.MoveChild(layerControl, index);
    }

    public void Move(IReadOnlyList<int> src, IReadOnlyList<int> dst)
    {
        int srcIdx = src[0];
        int dstIdx = dst[0];
        _rootControl.MoveChild(_rootControl.GetChild(srcIdx), dstIdx);
    }

    public void RemoveFree(Entity layerE)
    {
        // TODO: Warning: I'm being lazy to create a dedicated class for the layer control here.
        var layerControl = layerE.Get<Control>();
        layerE.Remove<Control>();
        var subscription = _subscriptions[layerControl];
        subscription.Dispose();
        _subscriptions.Remove(layerControl);
        layerControl.QueueFree();
    }
    
    private void OnDragStart(Control srcLayer, InputEventMouseMotion motion)
    {
        
    }

    private void OnDragging(Control _, InputEventMouseMotion e)
    {
        if (_mouseHoveringLayer == null)
        {
            if (_visibleDragHint != null) _visibleDragHint.Visible = false;
            _visibleDragHint = null;
            return;
        }
        
        var locPos = _mouseHoveringLayer.GetLocalMousePosition();
        var size = _mouseHoveringLayer.Size;
            
        var sep = size.Y / 2; // separation on whether the drop target is above or below the hovering layer.
        var hintToShow = _mouseHoveringLayer.GetNode<HSeparator>(locPos.Y < sep ? "%AboveHint" : "%BelowHint");
        if (_visibleDragHint == hintToShow) return;
        if (_visibleDragHint != null) _visibleDragHint.Visible = false;
        hintToShow.Visible = true;
        _visibleDragHint = hintToShow;
    }

    private void OnDragEnd(Control srcLayer, InputEventMouseButton button)
    {
        // Drag hint
        if(_visibleDragHint != null) _visibleDragHint.Visible = false;
        _visibleDragHint = null;
        
        // Move layer
        if(_mouseHoveringLayer == null || ReferenceEquals(_mouseHoveringLayer, srcLayer))
        {
            _mouseHoveringLayer = null;
            return;
        }

        var srcIndex = srcLayer.GetIndex();
        var dstIndex = _mouseHoveringLayer.GetIndex();
        if (srcIndex < dstIndex) dstIndex--; // after removing the source layer, the destination index is shifted left by 1.
        var locPos = _mouseHoveringLayer.GetLocalMousePosition();
        var size = _mouseHoveringLayer.Size;
        var sep = size.Y / 2;
        if (locPos.Y >= sep) dstIndex++; // insert after the hovering layer.
        
        new MoveLayerCmd([srcIndex], [dstIndex]).Commit();
    }
    
    public void SetWorkingLayerNoSignal(Entity layerE)
    {
        _workingLayerButtonGroup.GetPressedButton()?.SetPressedNoSignal(false);
        if (layerE.IsNull()) return;
        var layerControl = layerE.Get<Control>();
        var activeButton = layerControl.GetNode<CheckBox>("%Active");
        // Note: button group will not be updated.
        activeButton.SetPressedNoSignal(true);
    }
}