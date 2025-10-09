using Ciallo.Data;
using Ciallo.Rendering;
using Massive;

namespace Ciallo.Command;

public class ChangeStrokeBrushCmd(Entity strokeE, Entity newBrushE) : CommandBase
{
    private Entity _oldBrushE;
    
    public override void Do()
    {
        // Data
        var wrapper = strokeE.Get<StrokeBrush>();
        _oldBrushE = wrapper.Value;
        wrapper.Value = newBrushE;
        
        // View
        strokeE.Get<StrokeView>().Material = newBrushE.IsNotNull() ? 
            newBrushE.Get<BrushMaterial>() : BrushMaterial.MissingBrushMaterial;
    }

    public override void Undo()
    {
        // View
        strokeE.Get<StrokeView>().Material = _oldBrushE.IsNotNull() ? 
            _oldBrushE.Get<BrushMaterial>() : BrushMaterial.MissingBrushMaterial;
        
        // Data
        var wrapper = strokeE.Get<StrokeBrush>();
        wrapper.Value = _oldBrushE;
    }
}