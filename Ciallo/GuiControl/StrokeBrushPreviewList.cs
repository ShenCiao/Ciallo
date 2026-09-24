using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

[SceneTree, Instantiable]
public partial class StrokeBrushPreviewList : Container
{
    protected readonly Dictionary<Entity, Control> PreviewMap = [];
    protected ISynchronizedView<Entity, Control> SyncView;
    protected ObservableList<Entity> Brushes;
    private Entity _selectedBrush;
    public Entity Document;
    private CompositeDisposable _brushesSubs;
    private CompositeDisposable _workingBrushSubs;
    public readonly Subject<Entity> BrushClicked = new();

    public void Init(Entity document)
    {
        Document = document;
        var sm = Document.Get<SelectionManager>();
        var bm = Document.Get<BrushManager>();
        Bind(bm.StrokeBrushes, sm.WorkingStrokeBrush);
    }

    public void Init() { }

    public void Bind(ObservableList<Entity> brushes, ReactiveProperty<Entity> workingBrush)
    {
        BindBrushes(brushes);
        BindWorkingBrush(workingBrush);
    }

    public void BindBrushes(ObservableList<Entity> brushes)
    {
        Brushes = brushes;
        _brushesSubs?.Dispose();
        _brushesSubs = new();
        SyncView = brushes.CreateView(GetOrCreateBrushPreview);
        SyncView.AddTo(_brushesSubs);

        PreviewList.ObserveChildren(SyncView.ToNotifyCollectionChanged());

        PreviewList.SignalAsObservable<int, int>(DynamicGridItemList.SignalName.Moved)
            .Subscribe(tup => brushes.Move(tup.Item1, tup.Item2))
            .AddTo(_brushesSubs);
        PreviewList.SignalAsObservable<int>(DynamicGridItemList.SignalName.ItemClicked)
            .Subscribe(idx => BrushClicked.OnNext(SyncView.Filtered.ElementAt(idx).Value))
            .AddTo(_brushesSubs);
    }

    public void BindWorkingBrush(ReactiveProperty<Entity> workingBrush)
    {
        _workingBrushSubs?.Dispose();
        _workingBrushSubs = new();
        workingBrush.Subscribe(Select).AddTo(_workingBrushSubs);

        PreviewList.SignalAsObservable<int>(DynamicGridItemList.SignalName.ItemClicked)
            .Subscribe(idx => workingBrush.Value = SyncView.Filtered.ElementAt(idx).Value)
            .AddTo(_workingBrushSubs);
    }

    public void Select(Entity e)
    {
        _selectedBrush = e;
        PreviewList.SelectedControl = e.IsNull ? null : GetOrCreateBrushPreview(e);
        CopyButton.Disabled = e.IsNull;
        RemoveButton.Disabled = e.IsNull;
    }

    private Control GetOrCreateBrushPreview(Entity e)
    {
        if (PreviewMap.TryGetValue(e, out var box))
        {
            return box;
        }
        box = CreateBrushPreview(e);
        PreviewMap.Add(e, box);
        e.OnDelete += ent => PreviewMap.Remove(ent);
        return box;
    }

    private Control CreateBrushPreview(Entity e)
    {
        var wrapper = new PanelContainer()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        var checkerboard = new ColorRect
        {
            Material = AutoloadRendering.CheckerboardMaterial,
            Color = Colors.Transparent,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        wrapper.AddChild(checkerboard);

        var textureRect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = e.Get<ViewportTexture>(),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        wrapper.AddChild(textureRect);

        var nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        nameLabel.AddThemeConstantOverride("outline_size", 8);
        var nameWrapper = new MarginContainer()
        {
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
        };
        nameWrapper.AddThemeConstantOverride("margin_bottom", 12);
        nameWrapper.AddThemeConstantOverride("margin_right", 8);
        nameWrapper.AddChild(nameLabel);
        wrapper.AddChild(nameWrapper);

        var setting = e.Get<StrokeBrushSetting>();
        setting.Name.Subscribe(name => nameLabel.Text = name).AddTo(e);

        wrapper.QueueFreeWith(e);
        return wrapper;
    }

    public override void _Ready()
    {
        Select(_selectedBrush);
        CopyButton.Pressed += () => OnAddOrCopyButtonPressed(_selectedBrush);

        RemoveButton.Pressed += () =>
        {
            var oldE = _selectedBrush;
            if (oldE.IsNull) return;
            var es = SyncView.Filtered.Select(tup => tup.Value).ToList();
            var oldIdx = es.IndexOf(oldE);
            if (oldIdx == -1) return;
            int nextIdx = oldIdx == es.Count - 1 ? oldIdx - 1 : oldIdx + 1;
            Entity nextWorking = nextIdx == -1 ? Entity.Null : es[nextIdx];

            var command = new CommandBuilder("Delete Stroke Brush", Document);
            if (Document.Get<SelectionManager>().WorkingStrokeBrush.Value == oldE)
                command.SetProperty(e => e.Get<SelectionManager>().WorkingStrokeBrush, nextWorking);
            command.SetTarget(oldE).DeleteBrush().Commit();
        };

        EditButton.Pressed += () => Document.Get<StrokeBrushEditor>().Popup();
    }

    private void OnAddOrCopyButtonPressed(Entity copyE = default)
    {
        var brushE = Document.World.Create();
        new CommandBuilder("New Stroke Brush", brushE)
            .NewStrokeBrush(copyE)
            .SetTarget(Document)
            .SetProperty(e => e.Get<SelectionManager>().WorkingStrokeBrush, brushE)
            .Commit();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            SyncView?.Dispose();
            _brushesSubs?.Dispose();
            _workingBrushSubs?.Dispose();
        }
    }
}
