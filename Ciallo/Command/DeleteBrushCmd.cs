using Ciallo.Data;
using Frent;

namespace Ciallo.Command;

[CommandBuilder]
public class DeleteBrushCmd : CommandBase
{
    private int _index;
    public CommandBuilder SetNullBrushCmd = new();

    public override void BeforeFirstDo(Entity targetE)
    {
        if (targetE.Has<StrokeBrushSetting>())
        {
            _index = Document.Get<BrushManager>().StrokeBrushes.IndexOf(targetE);
            var query = targetE.World.CreateQuery().With<StrokeSetting>().Tagged<ToSerializeTag>().Build();
            foreach (var strokeE in query.EnumerateWithEntities())
            {
                if (strokeE.Get<StrokeSetting>().Brush.Value != targetE) continue;
                SetNullBrushCmd.SetTarget(strokeE)
                    .SetProperty(e => e.Get<StrokeSetting>().Brush, Entity.Null);
            }
        }
        else if (targetE.Has<FillBrushSetting>())
        {
            _index = Document.Get<BrushManager>().VectorFillBrushes.IndexOf(targetE);
            var markerQuery = targetE.World.CreateQuery().With<VectorFillMarkerSetting>().Tagged<ToSerializeTag>().Build();
            foreach (var markerE in markerQuery.EnumerateWithEntities())
            {
                if (markerE.Get<VectorFillMarkerSetting>().BrushE.Value != targetE) continue;
                SetNullBrushCmd.SetTarget(markerE)
                    .SetProperty(e => e.Get<VectorFillMarkerSetting>().BrushE, Entity.Null);
            }

            var polygonQuery = targetE.World.CreateQuery().With<FilledPolygonSetting>().Tagged<ToSerializeTag>().Build();
            foreach (var polygonE in polygonQuery.EnumerateWithEntities())
            {
                if (polygonE.Get<FilledPolygonSetting>().BrushE.Value != targetE) continue;
                SetNullBrushCmd.SetTarget(polygonE)
                    .SetProperty(e => e.Get<FilledPolygonSetting>().BrushE, Entity.Null);
            }
        }
    }

    public override void OnDeletedAsUndo() => TargetE.Delete();

    public override void Do(Entity brushE)
    {
        SetNullBrushCmd.Do();

        // Data
        var bm = Document.Get<BrushManager>();
        bm.StrokeBrushes.Remove(brushE);
        bm.VectorFillBrushes.Remove(brushE);
        brushE.Detach<ToSerializeTag>();
    }

    public override void Undo(Entity brushE)
    {
        // Data
        brushE.Tag<ToSerializeTag>();
        var bm = Document.Get<BrushManager>();

        if (brushE.Has<StrokeBrushSetting>())
            bm.StrokeBrushes.Insert(_index, brushE);
        if (brushE.Has<FillBrushSetting>())
            bm.VectorFillBrushes.Insert(_index, brushE);

        SetNullBrushCmd.Undo();
    }
}