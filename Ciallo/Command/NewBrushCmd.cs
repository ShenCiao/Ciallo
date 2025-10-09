using System.Collections.Generic;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;
using Massive;
using R3;

namespace Ciallo.Command;

public class NewBrushCmd : CommandBase
{
    public Entity BrushE;
    private readonly BrushSetting _setting;

    public NewBrushCmd(BrushSetting setting = null)
    {
        _setting = setting?.Clone() ?? new BrushSetting();
        _setting.Labels.Remove(BrushLabel.BuiltIn);
        
        // Dirty hack
        AppBrushLibrary.SelectedIndex.Value = -1;
    }
    
    public override IEnumerable<Entity> DoRefEntities => ToEnumerable(BrushE);
    
    public override void Do()
    {
        InitEntity();
        // Data
        BrushE.Add<ToSerializeTag>();
        var bm = Document.Get<BrushManager>();
        bm.Add(BrushE);
        
        // Material
        var material = new BrushMaterial();
        material.ObserveBrushSetting(BrushE.Get<BrushSetting>());
        BrushE.Set(material);
        
        // UI
        // Note: Should have a dedicate custom widget to handle this.
        var setting = BrushE.Get<BrushSetting>();
        var list = Document.Get<DocumentBrushList>();
        
        list.AddItem(setting.Name.Value);
        var sub = setting.Name.Subscribe(s =>
        {
            var idx = bm.Brushes.IndexOf(BrushE);
            list.SetItemText(idx, s);
        });
        var callableSub = Callable.From(() => sub.Dispose());
        list.SetItemMetadata(list.ItemCount - 1, callableSub);
    }

    public override void Undo()
    {
        // UI
        var bm = Document.Get<BrushManager>();
        var idx = bm.Brushes.IndexOf(BrushE);
        var list = Document.Get<DocumentBrushList>();
        var callableSub = (Callable)list.GetItemMetadata(idx);
        callableSub.Call();
        list.RemoveItem(idx);
        
        // Material
        // Note: Material is RefCounted, cannot be manually freed
        BrushE.Remove<BrushMaterial>();
        
        // Data
        bm.Remove(BrushE);
        BrushE.Remove<ToSerializeTag>();
    }

    public Entity InitEntity()
    {
        if (BrushE.IsNull())
        {
            BrushE = WorkingWorld.CreateEntity();
            BrushE.Set(_setting);
        }

        return BrushE;
    }
}