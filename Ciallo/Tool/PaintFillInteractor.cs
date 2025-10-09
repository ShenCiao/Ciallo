using Ciallo.Data;
using Ciallo.NodeControl;
using Massive;

namespace Ciallo.Tool;

public class PaintFillInteractor : InteractorBase
{
    public override bool CanInteract 
    {
        get
        {
            var l = SelectionManager.WorkingLayer.Value;
            return l.IsNotNull() && l.Has<PolylineLayerSetting>();
        }
    }

    public override void Start(CursorButtonData data)
    {
        
    }
}