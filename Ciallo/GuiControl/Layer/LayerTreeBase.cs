using System;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

/// <summary>
/// Abstract base for LayerTree and TrackHeaderTree.
/// Shares all drag-drop, block-binding, and scroll logic.
/// Concrete subclasses differ only in the Frent component types they read/write
/// (<see cref="GetWrapper"/>, <see cref="GetBlock"/>) and whether the dropdown arrow
/// is shown for CelFolders (<see cref="ShouldShowDropdownArrow"/>).
/// </summary>
public abstract partial class LayerTreeBase : ScrollContainer
{
    protected readonly ButtonGroup WorkingLayerButtonGroup = new();

    protected bool IsDragging;
    protected float ScrollSpeed;
    protected float ScrollAccum;
    /// <summary>
    /// Cached once per drag in <see cref="OnDragStart"/>.
    /// True when the dragged layer's subtree contains a CelFolder.
    /// </summary>
    protected bool DraggedSubtreeHasCelFolder;
    private bool _dragCancelled;

    protected const float ScrollZone = 50f;
    protected const float MaxScrollSpeed = 280f;

    // Node refs — populated by subclass _Ready via InitBase()
    private Container _root;
    private StrokeRect _hinter;
    private Label _label;
    private LayerRightClickMenu _rightClickMenu;

    protected void InitBase()
    {
        _root = GetNode<Container>("%RootContainer");
        _hinter = GetNode<StrokeRect>("%DropHinter");
        _label = GetNode<Label>("%DragLabel");
        _rightClickMenu = GetNode<LayerRightClickMenu>("%LayerRightClickMenu");

        _root.QueueFreeChildren();
        _hinter.MouseFilter = MouseFilterEnum.Ignore;
        _rightClickMenu.PopupHide += HideContextTargetHinter;

        WorkingLayerButtonGroup.Pressed += button =>
        {
            var block = (ILayerBlock)button.GetOwner();
            var document = block.LayerEntity.Document;
            var selectionManager = document.Get<SelectionManager>();
            int oldFrame = selectionManager.CurrentFrame.Value;
            int newFrame = selectionManager.ComputeFrameForWorkingLayerSelection(block.LayerEntity);

            var cmd = new CommandBuilder("Set Working Layer", block.LayerEntity);
            if (newFrame != oldFrame)
                cmd.SetProperty(selectionManager.CurrentFrame, oldFrame, newFrame);
            cmd.SetWorkingLayer(recordCelSelectionPreference: true)
                .CommitToLatest();
        };
    }

    public override void _Process(double delta)
    {
        if (!IsDragging || ScrollSpeed == 0f) return;
        ScrollAccum += ScrollSpeed * (float)delta;
        int step = (int)ScrollAccum;
        if (step != 0)
        {
            ScrollVertical += step;
            ScrollAccum -= step;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsDragging) return;
        if (!AppHotkeys.Global.InteractionCancel.IsPressedBy(@event)) return;

        IsDragging = false;
        _dragCancelled = true;
        _hinter.Visible = false;
        _label.Visible = false;
        ScrollSpeed = 0f;
        ScrollAccum = 0f;
        GetViewport().SetInputAsHandled();
    }

    // ── Abstract factory methods ────────────────────────────────────────────

    /// <summary>Returns the <see cref="LayerWrapper"/> component stored on <paramref name="e"/>.</summary>
    protected abstract LayerWrapper GetWrapper(Entity e);

    /// <summary>Returns the layer header block component stored on <paramref name="e"/>.</summary>
    protected abstract ILayerBlock GetBlock(Entity e);

    /// <summary>Whether the dropdown arrow should be shown for <paramref name="e"/>.</summary>
    protected virtual bool ShouldShowDropdownArrow(Entity e) => e.Has<FolderLayerSetting>();

    /// <summary>Whether this tree exposes timeline-only layer actions.</summary>
    protected virtual bool ShouldShowTimelineLayerActions => false;

    // ── Block initialisation ────────────────────────────────────────────────

    protected void InitBlock(Entity e)
    {
        var commonSetting = e.Get<CommonLayerSetting>();
        var cmdM = e.Document.Get<CommandManager>();

        var subs = new CompositeDisposable();
        subs.AddTo(e);

        var wrapper = GetWrapper(e);
        var block = GetBlock(e);
        block.WorkingButton.ButtonGroup = WorkingLayerButtonGroup;
        block.VisibleButton
            .BindBool(commonSetting.IsVisible, subs)
            .RegisterUndo(cmdM, true);
        StyleBoxFlat markColorStyleBox = null;
        commonSetting.MarkColor.Subscribe(markColor =>
        {
            if (markColor == null)
            {
                block.VisibleButton.RemoveThemeStyleboxOverride("normal");
                block.VisibleButton.RemoveThemeStyleboxOverride("pressed");
                block.WorkingButton.RemoveThemeStyleboxOverride("normal");
                block.WorkingButton.RemoveThemeStyleboxOverride("pressed");
                return;
            }

            markColorStyleBox ??= new StyleBoxFlat();
            markColorStyleBox.BgColor = markColor.Value;
            block.VisibleButton.AddThemeStyleboxOverride("normal", markColorStyleBox);
            block.VisibleButton.AddThemeStyleboxOverride("pressed", markColorStyleBox);
            block.WorkingButton.AddThemeStyleboxOverride("normal", markColorStyleBox);
            block.WorkingButton.AddThemeStyleboxOverride("pressed", markColorStyleBox);
        }).AddTo(subs);
        var lineEdit = block.LabelLineEdit
            .BindString(commonSetting.Name, subs)
            .RegisterUndo(cmdM);

        if (ShouldShowDropdownArrow(e))
        {
            block.DropdownArrow.Visible = true;
            var property = e.Get<FolderLayerSetting>().IsExpanded;
            block.DropdownArrow
                .BindBool(property, subs)
                .RegisterUndo(cmdM, true);
            wrapper.ObserveIsExpanded(property, subs);
        }
        else
        {
            block.DropdownArrow.Visible = false;
            // No Folded binding — wrapper stays permanently collapsed so its children
            // (which are CelFolder cels shown as timeline track rows) are never visible here.
            wrapper.IsExpanded = false;
        }

        var guiInput = lineEdit
            .SignalAsObservable<InputEvent>(Control.SignalName.GuiInput)
            .Where(_ => !lineEdit.IsEditing());
        var leftMouse = guiInput
            .OfType<InputEvent, InputEventMouseButton>()
            .Where(button => button.ButtonIndex == MouseButton.Left);
        var rightMouse = guiInput
            .OfType<InputEvent, InputEventMouseButton>()
            .Where(button => button.ButtonIndex == MouseButton.Right && button.IsPressed());
        rightMouse.Subscribe(button =>
        {
            lineEdit.AcceptEvent();
            ShowContextTargetHinter(lineEdit);
            _rightClickMenu.Popup(block.LayerEntity, ShouldShowTimelineLayerActions);
        }).AddTo(e);

        // Single click without dragging or double click
        var singleClickObs = leftMouse
            .Where(button => button.IsPressed() || button.IsReleased())
            .Chunk(TimeSpan.FromMilliseconds(200))
            .Where(xs => xs.Length == 2 && xs.First().IsPressed() && xs.Last().IsReleased())
            .Select(xs => xs.First());
        singleClickObs.Subscribe(_ => block.WorkingButton.SetPressed(true)).AddTo(e);

        // Drag
        var mouseState = leftMouse.ToReadOnlyReactiveProperty();
        var dragStart = guiInput
            .Where(_ => mouseState.CurrentValue?.IsPressed() == true)
            .OfType<InputEvent, InputEventMouseMotion>()
            .Where(motion => motion.ButtonMask == MouseButtonMask.Left)
            .Where(motion => motion.GlobalPosition.DistanceTo(mouseState.CurrentValue.GlobalPosition) > 20)
            .Where(_ => !IsDragging && !_dragCancelled);
        dragStart.Subscribe(motion =>
        {
            IsDragging = true;
            OnDragStart(block, motion);
        }).AddTo(e);

        var dragging = guiInput
            .Where(_ => IsDragging)
            .OfType<InputEvent, InputEventMouseMotion>()
            .Where(motion => motion.ButtonMask == MouseButtonMask.Left);
        dragging.Subscribe(motion => OnDragging(block, motion)).AddTo(e);

        var dragEnd = leftMouse
            .Where(button => button.IsReleased());
        dragEnd.Subscribe(button =>
        {
            _dragCancelled = false;
            if (!IsDragging) return;
            IsDragging = false;
            OnDragEnd(block, button);
        }).AddTo(e);
    }

    // ── Drop classification ─────────────────────────────────────────────────

    private enum DropKind { None, FolderChild, Sibling }

    private readonly record struct DropTarget(
        DropKind Kind = DropKind.None,
        Entity ParentEntity = default,
        int InsertIndex = -1);

    private DropTarget ClassifyDrop(ILayerBlock draggedBlock)
    {
        var mousePos = GetViewport().GetMousePosition();
        var hoverBlock = HitTestBlock(mousePos);

        if (hoverBlock == null)
        {
            if (this.GetGlobalRect().HasPoint(mousePos))
            {
                var docE = AppDocumentManager.WorkingDocument.CurrentValue;
                return new(DropKind.FolderChild, docE, 0);
            }
            return default;
        }

        if (ReferenceEquals(hoverBlock, draggedBlock))
            return default;

        var draggedEntity = draggedBlock.LayerEntity;
        var hoverEntity = hoverBlock.LayerEntity;
        var hoverTreeNode = hoverEntity.Get<LayerTreeNode>();
        var localPos = hoverBlock.Node.GetLocalMousePosition();
        var size = hoverBlock.Node.Size;

        var cursor = hoverEntity;
        while (!cursor.IsNull)
        {
            if (cursor == draggedEntity) return new(DropKind.None, default, -1);
            cursor = cursor.Get<LayerTreeNode>().ParentValue;
        }

        if (DraggedSubtreeHasCelFolder && hoverBlock.Wrapper.IsBeingCeled)
            return default;

        if (hoverBlock.IsFolder && localPos.Y > size.Y / 3f && localPos.Y <= size.Y * 2f / 3f)
        {
            if (DraggedSubtreeHasCelFolder && hoverBlock.IsCelFolder)
                return default;
            return new(DropKind.FolderChild, hoverEntity, 0);
        }

        var parentEntity = hoverTreeNode.ParentValue;
        int hoverIndex = hoverTreeNode.Index;
        int insertIndex = localPos.Y <= size.Y / 2f ? hoverIndex + 1 : hoverIndex;

        return new(DropKind.Sibling, parentEntity, insertIndex);
    }

    /// <summary>
    /// Returns the layer block whose header contains <paramref name="globalPos"/>, or null if none.
    /// Walks the visible <see cref="LayerWrapper"/> subtree directly instead of relying on
    /// MouseEntered/MouseExited, which are unreliable while a mouse button is held during a drag.
    /// </summary>
    private ILayerBlock HitTestBlock(Vector2 globalPos)
    {
        return Search(_root);

        ILayerBlock Search(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is not LayerWrapper wrapper) continue;

                var block = wrapper.Block;
                if (block != null && block.Node.GetGlobalRect().HasPoint(globalPos))
                    return block;

                // Folded wrappers push their content children out of view, so skip them.
                if (wrapper.IsExpanded)
                {
                    var hit = Search(wrapper);
                    if (hit != null) return hit;
                }
            }
            return null;
        }
    }

    // ── Drag handlers ───────────────────────────────────────────────────────

    private void OnDragStart(ILayerBlock draggedBlock, InputEventMouseMotion motion)
    {
        ScrollAccum = 0f;
        DraggedSubtreeHasCelFolder = draggedBlock.Wrapper.HasCelFolderInSubtree();
        _label.Text = draggedBlock.LayerEntity.Get<CommonLayerSetting>().Name.Value;
        _label.GlobalPosition = motion.GlobalPosition + new Vector2(16f, -8f);
        _label.Visible = true;
    }

    private void OnDragging(ILayerBlock draggedBlock, InputEventMouseMotion motion)
    {
        _label.GlobalPosition = motion.GlobalPosition + new Vector2(16f, -8f);

        var rect = GetGlobalRect();
        float mouseY = motion.GlobalPosition.Y;
        float distFromTop = mouseY - rect.Position.Y;
        float distFromBottom = rect.End.Y - mouseY;
        if (distFromTop < ScrollZone)
            ScrollSpeed = -MaxScrollSpeed * (1f - distFromTop / ScrollZone);
        else if (distFromBottom < ScrollZone)
            ScrollSpeed = MaxScrollSpeed * (1f - distFromBottom / ScrollZone);
        else
            ScrollSpeed = 0f;

        var dropTarget = ClassifyDrop(draggedBlock);

        if (dropTarget.Kind == DropKind.None)
        {
            _hinter.Visible = false;
            return;
        }

        if (dropTarget.Kind == DropKind.FolderChild)
        {
            if (!dropTarget.ParentEntity.IsDocument)
            {
                var labelLineEdit = GetBlock(dropTarget.ParentEntity).LabelLineEdit;
                _hinter.GlobalPosition = labelLineEdit.GlobalPosition;
                _hinter.Size = labelLineEdit.Size;
                _hinter.Visible = true;
            }
            else
            {
                var refBlock = GetBlock(dropTarget.ParentEntity.Get<LayerTreeNode>().Children[0]);
                float lineY = _root.GlobalPosition.Y + _root.Size.Y;
                PlaceHinterLine(dropTarget.ParentEntity, refBlock, lineY);
            }
            return;
        }

        // Sibling: horizontal line at the insertion boundary
        {
            var parentChildren = dropTarget.ParentEntity.Get<LayerTreeNode>().Children;
            int insertIndex = dropTarget.InsertIndex;

            ILayerBlock refBlock;
            float lineGlobalY;
            if (insertIndex < parentChildren.Count)
            {
                refBlock = GetBlock(parentChildren[insertIndex]);
                lineGlobalY = refBlock.Node.GlobalPosition.Y + refBlock.Node.Size.Y;
            }
            else
            {
                refBlock = GetBlock(parentChildren[^1]);
                lineGlobalY = refBlock.Node.GlobalPosition.Y;
            }

            PlaceHinterLine(dropTarget.ParentEntity, refBlock, lineGlobalY);
        }
    }

    /// <summary>
    /// Positions <see cref="_hinter"/> as a horizontal insertion line spanning from the
    /// parent's indent start to <paramref name="refBlock"/>'s right edge, at <paramref name="lineGlobalY"/>.
    /// </summary>
    private void PlaceHinterLine(Entity parentEntity, ILayerBlock refBlock, float lineGlobalY)
    {
        float startX = !parentEntity.IsDocument
            ? GetBlock(parentEntity).LabelLineEdit.GlobalPosition.X
            : refBlock.DropdownArrow.GlobalPosition.X;
        _hinter.GlobalPosition = new Vector2(startX, lineGlobalY - _hinter.Width / 2f);
        _hinter.Size = new Vector2(refBlock.Node.GlobalPosition.X + refBlock.Node.Size.X - startX, _hinter.Width);
        _hinter.Visible = true;
    }

    private void OnDragEnd(ILayerBlock draggedBlock, InputEventMouseButton button)
    {
        _hinter.Visible = false;
        _label.Visible = false;
        ScrollSpeed = 0f;
        ScrollAccum = 0f;

        var dropTarget = ClassifyDrop(draggedBlock);

        if (dropTarget.Kind == DropKind.None) return;

        var document = AppDocumentManager.WorkingDocument.CurrentValue;
        var draggedEntity = draggedBlock.LayerEntity;

        int insertIndex = dropTarget.InsertIndex;
        var draggedTreeNode = draggedEntity.Get<LayerTreeNode>();
        var oldParentE = draggedTreeNode.ParentValue;
        var newParentE = dropTarget.ParentEntity;
        if (oldParentE == newParentE && draggedTreeNode.Index < insertIndex)
            insertIndex--;

        var cmd = new CommandBuilder("Move Layer", document);

        int[] exposureFrames = [];
        if (oldParentE != newParentE && draggedEntity.Tagged<CelTag>())
        {
            exposureFrames = oldParentE.Get<FolderLayerSetting>().Exposures
                .Where(pair => pair.Value == draggedEntity)
                .Select(pair => pair.Key)
                .ToArray();

            if (exposureFrames.Length > 0)
                cmd.SetTarget(oldParentE)
                    .SetObservableCollection(
                        e => e.Get<FolderLayerSetting>().Exposures,
                        exposures =>
                        {
                            foreach (int frame in exposureFrames)
                                exposures.Remove(frame);
                        });
        }

        cmd.SetTarget(document)
            .MoveLayer(draggedEntity, newParentE, insertIndex);

        if (exposureFrames.Length > 0 && newParentE.TryGet<FolderLayerSetting>()?.IsCelFolder == true)
        {
            cmd.SetTarget(newParentE)
                .SetObservableCollection(
                    e => e.Get<FolderLayerSetting>().Exposures,
                    exposures =>
                    {
                        foreach (int frame in exposureFrames)
                        {
                            if (!exposures.ContainsKey(frame))
                                exposures.Add(frame, draggedEntity);
                        }
                    });
        }

        cmd.Commit();
    }

    public void SetWorkingLayerNoSignal(Entity layerE)
    {
        WorkingLayerButtonGroup.GetPressedButton()?.SetPressedNoSignal(false);
        if (layerE.IsNull || layerE.IsDocument) return;
        var block = GetBlock(layerE);
        // Warning note: button group will not be updated by SetPressedNoSignal.
        block.WorkingButton.SetPressedNoSignal(true);
    }

    private void ShowContextTargetHinter(Control target)
    {
        _hinter.GlobalPosition = target.GlobalPosition;
        _hinter.Size = target.Size;
        _hinter.Visible = true;
    }

    private void HideContextTargetHinter()
    {
        if (!IsDragging)
            _hinter.Visible = false;
    }
}
