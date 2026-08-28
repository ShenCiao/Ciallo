using Ciallo.Data;
using Ciallo.Tool;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

public partial class ToolButtonPanelContainer : Container
{
    public override void _Ready()
    {
        this.QueueFreeChildren();

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
