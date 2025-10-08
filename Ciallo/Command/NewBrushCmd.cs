using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;
using R3;

namespace Ciallo.Command;

public class NewBrushCmd : CommandBase
{
    public Entity BrushE = Entity.Null;
    private readonly BrushSetting _setting;

    /// <summary>
    /// Create a new brush command with an optional setting.
    /// If setting is provided, it will be cloned and used for the new brush.
    /// </summary>
    public NewBrushCmd(BrushSetting setting = null)
    {
        _setting = setting?.Clone() ?? new BrushSetting();
        _setting.Labels.Remove(BrushLabel.BuiltIn);
        
        // Dirty hack
        AppBrushLibrary.SelectedIndex.Value = -1;
    }

    /// <summary>
    /// Create a new brush command with an existing entity.
    /// This is useful for deserialization where the entity already has data components.
    /// </summary>
    public NewBrushCmd(Entity brushE)
    {
        BrushE = brushE;
        _setting = brushE.Has<BrushSetting>() ? brushE.Get<BrushSetting>() : new BrushSetting();
        
        // Dirty hack
        AppBrushLibrary.SelectedIndex.Value = -1;
    }
    
    public override IEnumerable<Entity> DoRefEntities => ToEnumerable(BrushE);
    
    public override void Do()
    {
        InitEntity();
        
        // Data
        if (!BrushE.Has<ToSerializeTag>())
        {
            BrushE.Add(new ToSerializeTag());
            var bm = Document.Get<BrushManager>();
            bm.Add(BrushE);
        }
        
        // Material
        if (!BrushE.Has<BrushMaterial>())
        {
            var material = new BrushMaterial();
            material.ObserveBrushSetting(BrushE.Get<BrushSetting>());
            BrushE.Add(material);
        }
        
        // UI
        // Note: Should have a dedicate custom widget to handle this.
        var setting = BrushE.Get<BrushSetting>();
        var list = Document.Get<DocumentBrushList>();
        var bm2 = Document.Get<BrushManager>();
        
        // Check if this brush is already in the list
        var existingIdx = bm2.Brushes.IndexOf(BrushE);
        if (existingIdx == -1 || existingIdx >= list.ItemCount)
        {
            list.AddItem(setting.Name.Value);
            var sub = setting.Name.Subscribe(s =>
            {
                var idx = bm2.Brushes.IndexOf(BrushE);
                list.SetItemText(idx, s);
            });
            var callableSub = Callable.From(() => sub.Dispose());
            list.SetItemMetadata(list.ItemCount - 1, callableSub);
        }
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
        if (BrushE == Entity.Null)
        {
            BrushE = WorkingWorld.Create();
            BrushE.Add(_setting);
        }

        return BrushE;
    }
}