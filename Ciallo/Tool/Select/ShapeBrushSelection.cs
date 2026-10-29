using System;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using ObservableCollections;
using R3;

namespace Ciallo.Tool;

public enum ShapeBrushKind { Stroke, Fill }

public sealed class ShapeBrushSelection : IDisposable
{
    public readonly ReactiveProperty<ImmutableArray<Entity>> UsedBrushes = new([]);
    public readonly ReactiveProperty<ImmutableArray<Entity>> SelectedBrushes = new([]);
    public readonly ObservableList<Entity> DocumentBrushes;
    private readonly Entity _document;
    private readonly SelectionManager _selection;
    private readonly ShapeBrushKind _kind;
    private readonly CompositeDisposable _subscriptions = new();
    private CompositeDisposable _layerSubscriptions = new();
    private CompositeDisposable _shapeSubscriptions = new();
    private Entity[] _shapes = [];

    public ShapeBrushSelection(Entity document, ShapeBrushKind kind)
    {
        _document = document;
        _kind = kind;
        _selection = document.Get<SelectionManager>();
        var brushes = document.Get<BrushManager>();
        DocumentBrushes = kind == ShapeBrushKind.Stroke ? brushes.StrokeBrushes : brushes.VectorFillBrushes;
        DocumentBrushes.ObserveChanged().Subscribe(_ => Refresh()).AddTo(_subscriptions);
        _selection.SelectedShapes.ObserveChanged().Subscribe(_ => RefreshSelection()).AddTo(_subscriptions);
        _selection.PrimaryLayer.Subscribe(layer =>
        {
            _layerSubscriptions.Dispose();
            _layerSubscriptions = new();
            if (!layer.IsNull)
                layer.Get<LayerTreeNode>().ObserveMutation().Subscribe(_ => BindShapes(layer)).AddTo(_layerSubscriptions);
            BindShapes(layer);
        }).AddTo(_subscriptions);
    }

    private bool MatchesKind(Entity shape) => _kind == ShapeBrushKind.Stroke
        ? shape.Has<StrokeSetting>()
        : shape.Has<FilledPolygonSetting>() || shape.Has<VectorFillMarkerSetting>();

    private ReactiveProperty<Entity> Brush(Entity shape)
    {
        if (_kind == ShapeBrushKind.Stroke) return shape.Get<StrokeSetting>().Brush;
        return shape.Has<FilledPolygonSetting>()
            ? shape.Get<FilledPolygonSetting>().BrushE
            : shape.Get<VectorFillMarkerSetting>().BrushE;
    }

    private void BindShapes(Entity layer)
    {
        _shapeSubscriptions.Dispose();
        _shapeSubscriptions = new();
        _shapes = layer.IsNull ? [] : layer.Get<LayerTreeNode>().Children.Where(MatchesKind).ToArray();
        foreach (var shape in _shapes)
            Brush(shape).Skip(1).Subscribe(_ => Refresh()).AddTo(_shapeSubscriptions);
        Refresh();
    }

    private void Refresh()
    {
        var used = _shapes.Select(shape => Brush(shape).Value).ToHashSet();
        UsedBrushes.Value = [.. DocumentBrushes.Where(used.Contains), .. used.Contains(Entity.Null) ? new[] { Entity.Null } : []];
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        var selected = _selection.SelectedShapes.ToHashSet();
        SelectedBrushes.Value = [.. _shapes.Where(selected.Contains).Select(shape => Brush(shape).Value).Distinct()];
    }

    public void Toggle(Entity brush)
    {
        var matching = _shapes.Where(shape => Brush(shape).Value == brush).ToArray();
        var selected = _selection.SelectedShapes;
        if (matching.Any(selected.Contains))
        {
            foreach (var shape in matching) selected.Remove(shape);
        }
        else
            selected.AddRange(matching);
    }

    public void ChangeBrush(Entity brush)
    {
        var selected = _selection.SelectedShapes.ToHashSet();
        var command = new CommandBuilder(_kind == ShapeBrushKind.Stroke
            ? "Set Selected Stroke Brush" : "Set Selected Vector Fill Brush", _document);
        foreach (var shape in _shapes.Where(selected.Contains))
            if (Brush(shape).Value != brush)
                command.SetTarget(shape).SetProperty(Brush, brush);
        command.Commit();
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _layerSubscriptions.Dispose();
        _shapeSubscriptions.Dispose();
        UsedBrushes.Dispose();
        SelectedBrushes.Dispose();
    }
}
