using System.Collections.Generic;
using Ciallo.Command;
using Ciallo.Rendering;
using Godot;
using Massive;

namespace Ciallo.Data;

public class SetStrokeGeometryCmd : CommandBase
{
    private readonly StrokeGeometry _newGeometry;
    private StrokeGeometry _oldGeometry;
    private readonly Entity _strokeE;

    public SetStrokeGeometryCmd(Entity strokeE, IReadOnlyList<Vector2> newPoints, IReadOnlyList<float> newRadii)
    {
        _strokeE = strokeE;
        _newGeometry = new()
        {
            Points = [..newPoints],
            Radii = [..newRadii],
        };
    }
    
    public SetStrokeGeometryCmd(Entity strokeE, StrokeGeometry newGeometry)
    {
        _strokeE = strokeE;
        _newGeometry = newGeometry;
    }

    public override void Do()
    {
        // Data
        _oldGeometry ??= _strokeE.Get<StrokeGeometry>();
        _strokeE.Set(_newGeometry);
        
        // View
        _strokeE.Get<StrokeView>().SetGeometry(_newGeometry.Points, _newGeometry.Radii);
        
        // Overlay
        _strokeE.Get<StrokeOverlay>().SetGeometry(_newGeometry.Points, _newGeometry.Radii);
    }

    public override void Undo()
    {
        // Overlay
        _strokeE.Get<StrokeOverlay>().SetGeometry(_oldGeometry.Points, _oldGeometry.Radii);

        // View
        _strokeE.Get<StrokeView>().SetGeometry(_oldGeometry.Points, _oldGeometry.Radii);
        
        // Data
        _strokeE.Set(_oldGeometry);
    }
}