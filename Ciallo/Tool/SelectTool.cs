using Massive;
using Ciallo.Widget;

namespace Ciallo.Tool;

public partial class SelectTool : CommonToolBase
{
    public readonly StrokeSelectionHintInteractor HintInteractor = new();
    
    public override InteractorBase HoveringInteractor => HintInteractor;
    public override void DrawProperty(PropertyContainer container, Entity Document)
    {
        
    }
}
