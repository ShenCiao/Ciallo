using Massive;
using Ciallo.Tool;
using Ciallo.Widget;
using Godot;
using Humanizer;

public abstract partial class ToolButtonBase : Button
{
    public virtual string ToolName => GetType().Name.Humanize();
    
    /// <remark>
    /// Shen: I guess this is the only design to violate the "who create who delete" rule
    /// </remark>
    public abstract void DrawProperty(PropertyContainer container, Entity document);
    
    public override void _EnterTree()
    {
        ButtonGroup = AppToolManager.ToolButtonGroup;
    }

    public override string ToString() => ToolName;
}