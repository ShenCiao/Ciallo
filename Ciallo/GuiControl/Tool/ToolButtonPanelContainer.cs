using Ciallo.Data;
using Ciallo.Tool;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

[Tool]
public partial class ToolButtonPanelContainer : Container
{
    public override void _Ready()
    {
        this.QueueFreeChildren();

        if (Engine.IsEditorHint())
        {
            var panel = ToolButtonPanel.Instantiate();
            AddChild(panel);
        }

        AppDocumentManager.WorkingDocument.Pairwise().Subscribe(pair =>
        {
            var previousDocument = pair.Previous;
            var document = pair.Current;
            if (!previousDocument.IsNull)
            {
                var previousPanel = previousDocument.Get<ToolButtonPanel>();
                previousPanel.UnpressActiveButton();
                previousPanel.QueueFree();
                previousDocument.Remove<ToolButtonPanel>();
            }

            if (document.IsNull) return;

            var panel = ToolButtonPanel.Instantiate();
            panel.Bind();
            document.Add(panel);
            AddChild(panel);
        }).AddTo(this);
    }
}
