using Ciallo.Data;
using Ciallo.Rendering;
using Massive;

namespace Ciallo.Command;

public class ChangeStrokeBrushCmd(Entity strokeE, Entity newBrushE) : CommandBase
{
    private Entity _oldBrushE = Entity.Null;
    
    public override void Do()
    {
        // Data
        var wrapper = strokeE.Get<StrokeBrush>();
        _oldBrushE = wrapper.Value;
        wrapper.Value = newBrushE;
        
        // View
        strokeE.Get<StrokeView>().Material = newBrushE != Entity.Null ? 
            newBrushE.Get<BrushMaterial>() : BrushMaterial.MissingBrushMaterial;
    }

    public override void Undo()
    {
        // View
        strokeE.Get<StrokeView>().Material = _oldBrushE != Entity.Null ? 
            _oldBrushE.Get<BrushMaterial>() : BrushMaterial.MissingBrushMaterial;
        
        // Data
        var wrapper = strokeE.Get<StrokeBrush>();
        wrapper.Value = _oldBrushE;
    }
}