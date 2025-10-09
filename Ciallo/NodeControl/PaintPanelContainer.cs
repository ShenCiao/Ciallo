using Ciallo.Data;
using Godot;
using Massive;

namespace Ciallo.NodeControl;

public partial class PaintPanelContainer : Control
{
    public PaintPanel CreateAddPaintPanel(Entity document)
    {
        var panel = PaintPanel.Instantiate(document.Get<DocumentSetting>());
        AddChild(panel);
        document.Set(panel);
        return panel;
    }
    
    public void RemoveFreePaintPanel(Entity document)
    {
        var panel = document.Get<PaintPanel>();
        document.Remove<PaintPanel>();
        panel.QueueFree();
    }
}