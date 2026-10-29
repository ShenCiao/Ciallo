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
public partial class VectorFillBrushPreviewList : Container
{
    protected readonly Dictionary<Entity, Control> PreviewMap = [];
    protected ISynchronizedView<Entity, Control> SyncView;
    protected ObservableList<Entity> Brushes;
    protected ReactiveProperty<Entity> WorkingBrush;
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
        Bind(bm.VectorFillBrushes, sm.WorkingVectorFillBrush);
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
        WorkingBrush = workingBrush;
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

    public void DrawNameProperty(PropertyContainer container)
    {
        var nameEdit = new LineEdit
        {
            FocusMode = FocusModeEnum.Click,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        var name = WorkingBrush.Select(e => e.TryGet<FillBrushSetting>()?.Name)
            .Flatten().AddTo(nameEdit);
        container.AddProperty("Name", nameEdit.BindString(name))
            .VisibleIf(WorkingBrush, Entity.IsNotNull);
    }

    private Control GetOrCreateBrushPreview(Entity e)
    {
        if (PreviewMap.TryGetValue(e, out var box))
        {
            return box;
        }
        box = BrushPreview.Fill(e).QueueFreeWith(e);
        PreviewMap.Add(e, box);
        e.OnDelete += ent => PreviewMap.Remove(ent);
        return box;
    }

    public override void _Ready()
    {
        Select(_selectedBrush);
        AddButton.Pressed += () => OnAddOrCopyButtonPressed();

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

            var command = new CommandBuilder("Delete Vector Fill Brush", Document);
            if (Document.Get<SelectionManager>().WorkingVectorFillBrush.Value == oldE)
                command.SetProperty(e => e.Get<SelectionManager>().WorkingVectorFillBrush, nextWorking);
            command.SetTarget(oldE).DeleteBrush().Commit();
        };
    }

    private void OnAddOrCopyButtonPressed(Entity copyE = default)
    {
        var brushE = Document.World.Create();
        new CommandBuilder("New Vector Fill Brush", brushE)
            .NewVectorFillBrush(copyE)
            .SetTarget(Document)
            .SetProperty(e => e.Get<SelectionManager>().WorkingVectorFillBrush, brushE)
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
