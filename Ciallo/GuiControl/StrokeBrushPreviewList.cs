using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

[SceneTree, Instantiable]
public partial class StrokeBrushPreviewList : Container
{
    private readonly Dictionary<Entity, Control> _previews = [];
    private readonly CompositeDisposable _subscriptions = new();
    private ObservableList<Entity> _brushes;
    private Entity _selectedBrush;
    private Entity _document;

    public void Init(Entity document)
    {
        _document = document;
        _brushes = document.Get<BrushManager>().StrokeBrushes;
        var workingBrush = document.Get<SelectionManager>().WorkingStrokeBrush;
        var view = _brushes.CreateView(GetOrCreateBrushPreview).AddTo(_subscriptions);
        PreviewList.ObserveChildren(view.ToNotifyCollectionChanged());

        PreviewList.SignalAsObservable<int, int>(DynamicGridItemList.SignalName.Moved)
            .Subscribe(tup => StrokeBrushActions.MoveDocumentBrush(_document, tup.Item1, tup.Item2))
            .AddTo(_subscriptions);
        PreviewList.SignalAsObservable<int>(DynamicGridItemList.SignalName.ItemClicked)
            .Subscribe(idx => workingBrush.Value = view.Filtered.ElementAt(idx).Value)
            .AddTo(_subscriptions);
        workingBrush.Subscribe(Select).AddTo(_subscriptions);
    }

    private void Select(Entity e)
    {
        _selectedBrush = e;
        PreviewList.SelectedControl = e.IsNull ? null : GetOrCreateBrushPreview(e);
        CopyButton.Disabled = e.IsNull;
        RemoveButton.Disabled = e.IsNull;
        EditButton.Disabled = e.IsNull;
    }

    private Control GetOrCreateBrushPreview(Entity e)
    {
        if (_previews.TryGetValue(e, out var box))
        {
            return box;
        }
        box = BrushPreview.Stroke(e).QueueFreeWith(e);
        _previews.Add(e, box);
        e.OnDelete += ent => _previews.Remove(ent);
        return box;
    }

    public override void _Ready()
    {
        Select(_selectedBrush);
        AddButton.Pressed += () => AppDialogHost.BrushLibrary.PopupCenteredClamped();
        CopyButton.Pressed += () => StrokeBrushActions.AddToDocument(_document,
            _selectedBrush.Get<StrokeBrushSetting>(), _brushes.Count);
        RemoveButton.Pressed += () => StrokeBrushActions.DeleteDocumentBrush(_document, _selectedBrush);
        EditButton.Pressed += () => _document.Get<StrokeBrushEditor>().CallDeferred(Window.MethodName.PopupCenteredClamped);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            _subscriptions.Dispose();
    }
}
