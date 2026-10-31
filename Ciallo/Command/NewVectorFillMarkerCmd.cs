using System;
using System.Collections.Generic;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.Command;

[CommandBuilder]
public class NewVectorFillMarkerCmd : NewShapeCmdBase
{
    public NewVectorFillMarkerCmd(Entity copyE = default, IReadOnlyDictionary<Entity, Entity> entityMap = null)
        : base(copyE, entityMap)
    {
    }

    public override void OnDeletedAsDo() => TargetE.Delete();

    protected override void AddDataComponents(Entity targetE)
    {
        var layerNode = new LayerTreeNode();
        targetE.Add(layerNode);

        var sampledPolyline = CopyE.IsNull
            ? new SampledPolyline()
            : CopyE.Get<SampledPolyline>().Clone();
        targetE.Add(sampledPolyline);

        var setting = CopyE.IsNull
            ? new VectorFillMarkerSetting()
            : CopyE.Get<VectorFillMarkerSetting>().Clone();
        setting.BrushE.Value = MapEntityRef(setting.BrushE.Value);
        targetE.Add(setting);
    }

    protected override void CreateRuntime(Entity targetE)
    {
        var layerNode = targetE.Get<LayerTreeNode>();
        var sampledPolyline = targetE.Get<SampledPolyline>();
        var setting = targetE.Get<VectorFillMarkerSetting>();

        // By design, polygons attached to fill markers are views,
        // Strokes and marker sprites attached are overlays.
        // View
        var polygonView = new PolygonFillView();
        targetE.AddNode(polygonView);
        setting.BrushE
            .Select(e => e.IsNull
                ? Observable.Return(Colors.Black)
                : e.Get<FillBrushSetting>().FillColor.AsObservable())
            .Switch()
            .Subscribe(polygonView.SetColor)
            .AddTo(targetE);

        // Polygon view — ArrReady emits whenever the arrangement is settled and safe to query.
        layerNode.Parent
            .Select(e => e.IsNull
                ? Observable.Return<Arrangement>(null)
                : e.Get<ArrangementManager>().ArrReady.AsObservable())
            .Switch()
            .CombineLatest(sampledPolyline.Positions.ThrottleLastFrame(1), ValueTuple.Create)
            .Subscribe(tuple =>
            {
                var (arr, positions) = tuple;
                if (positions.IsDefaultOrEmpty)
                    polygonView.Clear();
                else if (arr != null)
                    polygonView.SetPolygonWithQueryResult(arr, positions[0]);
                // else: arr mid-rebuild — keep last frame's polygon to avoid flicker.
            }).AddTo(targetE);

        // Overlay
        var wireframeOverlay = new PolylineWireframe() { Visible = false };
        targetE.AddNode(wireframeOverlay);

        sampledPolyline.Positions.ThrottleLastFrame(1).Subscribe(p =>
        {
            wireframeOverlay.SetGeometry(p);
        }).AddTo(targetE);

        var marker = new VectorFillMarkerView();
        targetE.AddNode(marker);

        setting.BrushE.Subscribe(brushE =>
        {
            if (brushE.IsNull)
            {
                VectorFillMarkerView.ApplyMissingBrush(polygonView, marker);
                return;
            }

            polygonView.MissingBrush = false;
        }).AddTo(targetE);
        setting.BrushE
            .Select(e => e.IsNull
                ? Observable.Return(ImageTexture.Dummy)
                : e.Get<FillBrushSetting>().MarkerTexture.AsObservable())
            .Switch()
            .Subscribe(texture => marker.Sprite.Texture = texture ?? ImageTexture.Dummy)
            .AddTo(targetE);
        setting.BrushE
            .Select(e => e.IsNull
                ? Observable.Return(Colors.Black)
                : e.Get<FillBrushSetting>().MarkerColor.AsObservable())
            .Switch()
            .Subscribe(marker.Sprite.SetModulate)
            .AddTo(targetE);
        // Dot hints at the polygon's fill color, so bind it to the same FillColor source as polygonView.
        setting.BrushE
            .Select(e => e.IsNull
                ? Observable.Return(Colors.Black)
                : e.Get<FillBrushSetting>().FillColor.AsObservable())
            .Switch()
            .Subscribe(c => marker.ColorDot.Color = c)
            .AddTo(targetE);

        sampledPolyline.ObserveShape().ThrottleLastFrame(1).Subscribe(v =>
        {
            marker.SetGeometry(v.Item1, v.Item2);
        }).AddTo(targetE);

        // Body
        var strokeBody = new Body();
        targetE.AddNode(strokeBody);

        Document.Get<WorldBody>().MakeScreenSize(strokeBody);

        sampledPolyline.ObserveShape().ThrottleLastFrame(1).Subscribe(v =>
        {
            strokeBody.ClearShapes();
            if (v.Item1.IsDefaultOrEmpty) return;

            strokeBody.Position = v.Item1[0];
            strokeBody.AddChild(new CollisionShape2D()
            {
                Shape = new RectangleShape2D { Size = 2f * v.Item2[0] * Vector2.One },
            });
        }).AddTo(targetE);

        // Layer tree events
        var events = layerNode.MovedReparentedAsAddedRemoved;
        events.Added.Subscribe(et =>
        {
            (int index, var layerE) = (et.Index, et.Parent);
            // View
            var layerView = layerE.Get<ShapeLayerView>();
            layerView.InsertNodeAt(polygonView, index);

            // Overlay
            var holder = layerE.Get<OverlayHolder>();
            holder.GetChild(0).AddChild(marker);
            holder.GetChild(1).AddChild(wireframeOverlay);

            // Body
            layerE.Get<BodyHolder>().InsertNodeAt(strokeBody, index);
        }).AddTo(targetE);

        events.Removed.Subscribe(_ =>
        {
            // Body
            strokeBody.RemoveFromParent();

            // Overlay
            marker.RemoveFromParent();
            wireframeOverlay.RemoveFromParent();

            // View
            polygonView.RemoveFromParent();
        }).AddTo(targetE);
    }

    public override void Do(Entity targetE)
    {
        targetE.Tag<ToSerializeTag>();
    }

    public override void Undo(Entity targetE)
    {
        Document.Get<SelectionManager>().SelectedShapes.Remove(targetE);

        targetE.Detach<ToSerializeTag>();
    }
}
