using System;
using Ciallo.Data;
using Massive;

namespace Ciallo.Command;

public class ChangeWorkingBrushCmd(Index idx) : CommandBase
{
    public Entity OldBrushE;
    public Entity NewBrushE;
    
    public override void Do()
    {
        var bm = Document.Get<BrushManager>();
        var sm = Document.Get<SelectionManager>();
        
        // data
        OldBrushE = sm.WorkingBrush.Value;
        NewBrushE = bm.Brushes[idx];
        sm.WorkingBrush.Value = NewBrushE;
        
        // UI
        var brushList = Document.Get<DocumentBrushList>();
        brushList.Select(idx.GetOffset(bm.Brushes.Count));
    }

    public override void Undo()
    {
        var bm = Document.Get<BrushManager>();
        var sm = Document.Get<SelectionManager>();
        
        // UI
        var brushList = Document.Get<DocumentBrushList>();
        if(OldBrushE.IsNull()) brushList.DeselectAll();
        else
        {
            var oldIdx = bm.Brushes.IndexOf(OldBrushE);
            if(oldIdx == -1) throw new InvalidOperationException("Old brush not found in brush manager.");
            brushList.Select(oldIdx);
        }
        
        // data
        sm.WorkingBrush.Value = OldBrushE;
    }
}