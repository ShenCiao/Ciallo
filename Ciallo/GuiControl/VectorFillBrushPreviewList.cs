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

/// <summary>
/// 
/// </summary>
/// <remarks>
/// Terrible code design here, duplicated with StrokeBrushPreview, refactor it once there is another change.
/// </remarks>
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
        box = CreateBrushPreview(e);
        PreviewMap.Add(e, box);
        e.OnDelete += ent => PreviewMap.Remove(ent);
        return box;
    }

    private PanelContainer CreateBrushPreview(Entity e)
    {
        var box = new PanelContainer().QueueFreeWith(e);
        var background = new ColorRect() { Material = AutoloadRendering.CheckerboardMaterial };
        box.AddChild(background);
        var markerPreview = new TextureRect()
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            CustomMinimumSize = new(32, 32),
        };
        var container = new CenterContainer();
        container.AddChild(markerPreview);
        box.AddChild(container);

        var nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 1,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            AnchorRight = 1,
            OffsetLeft = 4,
            OffsetRight = -4,
            OffsetTop = 16,
            OffsetBottom = 16,
            GrowVertical = GrowDirection.Both,
        };
        nameLabel.AddThemeConstantOverride("outline_size", 8);
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        var nameOverlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        nameOverlay.AddChild(nameLabel);
        box.AddChild(nameOverlay);

        var setting = e.Get<FillBrushSetting>();
        setting.Name.Subscribe(name => nameLabel.Text = name).AddTo(e);
        setting.MarkerTexture.Subscribe(markerPreview.SetTexture).AddTo(e);
        setting.MarkerColor.Subscribe(markerPreview.SetSelfModulate).AddTo(e);
        setting.FillColor.Subscribe(background.SetColor).AddTo(e);
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
