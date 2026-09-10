using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

/// <summary>
/// Each layer entity gets a full-width <see cref="TrackRowWrapper"/> whose title is a
/// <see cref="TrackRow"/> (HSplitContainer) containing both the
/// <see cref="TrackHeaderBlock"/> (left panel) and — for CelFolder layers — a
/// <see cref="CelTrack"/> (right panel).
/// The split offset of every <see cref="TrackRow"/> is kept in sync with HSplitRuler
/// via <see cref="SplitOffset"/>.
/// </summary>
[SceneTree(root: "Root"), Instantiable]
public partial class TrackTree : LayerTreeBase
{
    private int _splitOffset = 256;

    public override void _Ready()
    {
        InitBase();
    }

    protected override LayerWrapper GetWrapper(Entity e) => e.Get<TrackRowWrapper>();
    protected override ILayerBlock GetBlock(Entity e) => e.Get<TrackHeaderBlock>();
    protected override bool ShouldShowTimelineLayerActions => true;

    /// <summary>Exposes the root wrapper so <see cref="TimelinePanel"/> can register it on the document entity.</summary>
    public TrackRowWrapper RootWrapper => (TrackRowWrapper)RootContainer;

    /// <summary>Shared right-click menu for all <see cref="CelTrack"/> instances. Set by <see cref="TimelinePanel"/>.</summary>
    public CelTrackRightClickMenu RightClickMenu { get; set; }

    /// <summary>
    /// The split offset (in pixels) shared by all <see cref="TrackRow"/> instances in this tree.
    /// Set this whenever HSplitRuler is dragged.
    /// </summary>
    public int SplitOffset
    {
        get => _splitOffset;
        set
        {
            if (_splitOffset == value) return;
            _splitOffset = value;
            UpdateAllSplits(RootContainer, [value]);
        }
    }

    private static void UpdateAllSplits(Node node, int[] splitOffsets)
    {
        int childCount = node.GetChildCount();
        for (int i = 0; i < childCount; i++)
        {
            var child = node.GetChild(i);
            // Archetype rows are HSplitContainers added directly under a wrapper (not a TrackRow title).
            if (child is HSplitContainer archetypeSplit)
                archetypeSplit.SplitOffsets = splitOffsets;
            if (child is not TrackRowWrapper wrapper) continue;
            if (wrapper.Title is TrackRow row)
                row.SplitOffsets = splitOffsets;
            UpdateAllSplits(wrapper, splitOffsets);
        }
    }

    /// <summary>
    /// Creates a <see cref="TrackRowWrapper"/> + <see cref="TrackRow"/> for
    /// <paramref name="layerE"/> and wires all UI bindings via
    /// <see cref="LayerTreeBase.InitBlock"/>.
    /// For CelFolder layers a <see cref="CelTrack"/> is added to the right panel and
    /// bound to the layer's exposure table and the document's <see cref="TimelineSetting"/>.
    /// Call once per entity from its layer-tree-node <c>Added</c> event handler.
    /// </summary>
    public void Create(Entity layerE)
    {
        var wrapper = new TrackRowWrapper();
        var trackRow = TrackRow.New();
        trackRow.Configure(_splitOffset, wrapper);

        var folderSetting = layerE.TryGet<FolderLayerSetting>();
        if (folderSetting?.IsCelFolder == true)
        {
            var subs = new CompositeDisposable();
            subs.AddTo(layerE);
            trackRow.EnableCelTrack(layerE, RightClickMenu, subs);
            WireCelChildArchetypes(layerE, wrapper, folderSetting, subs);
        }

        wrapper.Title = trackRow;
        layerE.Add(trackRow.HeaderBlock);
        layerE.AddNode(wrapper);

        InitBlock(layerE);
    }

    /// <summary>
    /// Renders one archetype <see cref="LayerBlock"/> per distinct cel-child name under a cel folder,
    /// shown when the folder is expanded (the real cel rows stay hidden via
    /// <see cref="TrackRowWrapper.IsBeingCeled"/>). Reconciles on the debounced add/remove signals
    /// of <see cref="FolderLayerSetting.CelChildrenByName"/>: new key -> create+wire an archetype,
    /// removed key -> dispose+free its block. Surviving keys are left untouched.
    /// </summary>
    private void WireCelChildArchetypes(Entity layerE, TrackRowWrapper wrapper, FolderLayerSetting folderSetting, CompositeDisposable subs)
    {
        var celChildrenByName = folderSetting.CelChildrenByName;
        var sm = layerE.Document.Get<SelectionManager>();
        var blocks = new Dictionary<string, LayerBlock>();
        var blockSubs = new Dictionary<string, CompositeDisposable>();

        void CreateArchetype(string name)
        {
            if (blocks.ContainsKey(name)) return;

            var block = LayerBlock.New();
            // Selection operates on the corresponding layer in the currently exposed cel.
            block.WorkingButton.Visible = true;
            block.DropdownArrow.Visible = false;
            block.FolderIcon.Visible = false;
            block.LabelLineEdit.SubmitOnFocusExit(); // once: re-calling would stack FocusExited handlers.

            // Wrap in an HSplitContainer (block left, blank right) so the row obeys the shared
            // SplitOffset like every TrackRow; otherwise the block would stretch full-width across
            // the timeline column. UpdateAllSplits keeps the offset in sync.
            var split = new HSplitContainer { DraggingEnabled = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            split.AddThemeStyleboxOverride("split_bar_background", new StyleBoxEmpty());
            split.AddChild(block);
            split.AddChild(new Control());
            split.SplitOffsets = [_splitOffset];
            wrapper.AddChild(split);
            // ponytail: one level deeper than the cel-folder header (Wrapper.Level - 1) so an archetype
            // reads as an aggregated child row, not a sibling of the cel folder. LayerBlock._EnterTree
            // skips indent for a non-wrapper parent, so this assignment sticks (archetype rows never re-enter).
            block.Indent.Count = wrapper.Level;
            blocks[name] = block;

            BindArchetype(name, block);
        }

        // (Re)wire a block to a current representative of its name group. Called on create and on every
        // reconcile for surviving keys, so a block always reflects a member that is currently in the group
        // (handles rename-merge: the kept block re-picks a representative from the merged membership).
        void BindArchetype(string name, LayerBlock block)
        {
            if (blockSubs.Remove(name, out var oldSubs)) oldSubs.Dispose();
            if (!celChildrenByName.TryGetValue(name, out var members) || members.Count == 0) return;

            var bs = new CompositeDisposable();
            blockSubs[name] = bs;

            // Display mirrors a representative member; ground truth lives on the layers, the block stores
            // no value of its own. Undo/redo reverts members -> representative fires -> display follows,
            // so the block can never drift from the real state.
            // ponytail: representative is any current member (no "mixed" indicator); members that disagree
            // are not surfaced and keep their own values until the next explicit archetype edit.
            Entity rep = default;
            foreach (var m in members) { rep = m; break; }
            var repSetting = rep.Get<CommonLayerSetting>();
            repSetting.IsVisible.Subscribe(block.VisibleButton.SetPressedNoSignal).AddTo(bs);
            repSetting.Name.Subscribe(block.LabelLineEdit.SetText).AddTo(bs);

            // Input pushes to every current member as one undoable action.
            block.VisibleButton.OnToggledAsObservable()
                .Subscribe(v =>
                {
                    PushToMembers(name, e => e.Get<CommonLayerSetting>().IsVisible, v).Commit();
                }).AddTo(bs);
            block.LabelLineEdit.OnTextSubmittedAsObservable()
                .Subscribe(v =>
                {
                    var renameMembers = celChildrenByName[name];
                    var cmd = PushToMembers(name, e => e.Get<CommonLayerSetting>().Name, v);
                    if (renameMembers.Contains(sm.PrimaryLayer.CurrentValue))
                        cmd.SetProperty(folderSetting.PreferredNameForCelSelection, v);
                    cmd.Commit();
                }).AddTo(bs);

            Entity ResolveTarget()
            {
                var cel = folderSetting.CurrentExposedCel.CurrentValue;
                return cel.IsNull || cel.IsCelFolder
                    ? Entity.Null : cel.Get<LayerTreeNode>().GetLayerChildByName(name);
            }

            void SyncPressed()
            {
                var target = ResolveTarget();
                LayerSelectionActions.ShowSelection(block.WorkingButton,
                    !target.IsNull && sm.PrimaryLayer.CurrentValue == target,
                    !target.IsNull && sm.SelectedLayers.Value.Contains(target));
            }

            sm.SelectedLayers.CombineLatest(folderSetting.CurrentExposedCel, members.ObserveChanged().PrependDefault(), ValueTuple.Create)
                .DebounceFrame(1)
                .Subscribe(_ => SyncPressed()).AddTo(bs);
            SyncPressed();

            block.WorkingButton.OnToggledAsObservable().Subscribe(_ =>
            {
                var target = ResolveTarget();
                if (!target.IsNull) LayerSelectionActions.Toggle(target);
                SyncPressed();
            }).AddTo(bs);
            block.LabelLineEdit.SignalAsObservable<InputEvent>(Control.SignalName.GuiInput)
                .OfType<InputEvent, InputEventMouseButton>()
                .Where(_ => !block.LabelLineEdit.IsEditing())
                .Subscribe(button =>
            {
                var target = ResolveTarget();
                if (target.IsNull) return;
                if (button.ButtonIndex == MouseButton.Left && !button.Pressed)
                    LayerSelectionActions.SelectOnly(target);
                else if (button.ButtonIndex == MouseButton.Right && button.Pressed)
                {
                    block.LabelLineEdit.AcceptEvent();
                    ShowLayerMenu(target, block.LabelLineEdit);
                }
            }).AddTo(bs);
        }

        // Overwrite the chosen property on every current member of the named group, in one undoable action.
        CommandBuilder PushToMembers<T>(
            string name,
            System.Func<Entity, ReactiveProperty<T>> getProp,
            T value)
        {
            var members = celChildrenByName[name];
            var cmd = new CommandBuilder("Edit Cel Child Archetype");
            foreach (var member in members)
                cmd.SetTarget(member).SetProperty(getProp, getProp(member).Value, value);
            return cmd;
        }

        void RemoveArchetype(string name)
        {
            if (blockSubs.Remove(name, out var bs)) bs.Dispose();
            // Free the HSplitContainer wrapper (block's parent), not just the block, or the split is orphaned.
            if (blocks.Remove(name, out var block)) block.GetParent().QueueFree();
        }

        // Sort key for an archetype: the representative cel child's index within its cel
        // (LayerTreeNode.Index). Same representative pick as BindArchetype (first in the set), so order
        // and display agree. int.MaxValue parks a name with no live member at the end.
        int RepIndex(string name)
        {
            if (celChildrenByName.TryGetValue(name, out var members))
                foreach (var m in members)
                    return m.IsAlive ? m.Get<LayerTreeNode>().Index : int.MaxValue;
            return int.MaxValue;
        }

        // Order the archetype rows to mirror layer order: ascending RepIndex, matching the layer-panel
        // convention (lower index sits lower in the ReverseOrder stack).
        // Cel rows are safe because archetypes form a contiguous tail: they are only ever appended
        // (CreateArchetype's wrapper.AddChild), while cel rows occupy the low indices [0..numCels) via
        // InsertNodeAt(dataIndex), and every cel add/remove/move preserves that tail. So the slots the
        // archetypes occupy are a contiguous block above all cel rows; permuting within it never moves a
        // cel row (MoveChild shifts intervening nodes, but no cel row lies between two archetype slots).
        void ReorderArchetypes()
        {
            if (blocks.Count < 2) return;

            var ordered = new List<(Node split, int order)>(blocks.Count);
            foreach (var (name, block) in blocks)
                ordered.Add((block.GetParent(), RepIndex(name)));
            ordered.Sort((a, b) => a.order.CompareTo(b.order));

            var slots = new List<int>(ordered.Count);
            foreach (var (split, _) in ordered)
                slots.Add(split.GetIndex());
            slots.Sort();

            // Process ascending: slot k receives the k-th desired split. Targets are the pre-captured
            // sorted slots, so each move only shuffles not-yet-placed nodes, preserving placed ones.
            for (int k = 0; k < ordered.Count; k++)
                wrapper.MoveChild(ordered[k].split, slots[k]);
        }

        void Reconcile()
        {
            foreach (var name in new List<string>(blocks.Keys))
                if (!celChildrenByName.ContainsKey(name))
                    RemoveArchetype(name);
            foreach (var pair in celChildrenByName)
            {
                if (blocks.TryGetValue(pair.Key, out var block))
                    BindArchetype(pair.Key, block); // surviving key: re-pick representative (handles merge)
                else
                    CreateArchetype(pair.Key);
            }
            ReorderArchetypes();
        }

        celChildrenByName.ObserveChanged()
            .DebounceFrame(1)
            .Subscribe(_ => Reconcile())
            .AddTo(subs);

        Reconcile();
    }
}
