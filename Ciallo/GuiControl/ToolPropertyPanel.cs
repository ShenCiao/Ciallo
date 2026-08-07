using Ciallo.Data;
using Ciallo.Tool;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

public partial class ToolPropertyPanel : Container
{
    public VBoxContainer PropertyHolder;

    partial class DocumentToolPropertyContainer : VBoxContainer;

    public override void _Ready()
    {
        PropertyHolder = GetNode<VBoxContainer>("%PropertiesHolder");
        PropertyHolder.QueueFreeChildren();

        AppDocumentManager.WorkingDocument.Pairwise().Subscribe(pair =>
        {
            var previousDocument = pair.Previous;
            var document = pair.Current;
            if (!previousDocument.IsNull)
            {
                previousDocument.Get<DocumentToolPropertyContainer>().QueueFree();
                previousDocument.Remove<DocumentToolPropertyContainer>();
            }

            if (document.IsNull) return;

            var holderPerDocument = new DocumentToolPropertyContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            document.Add(holderPerDocument);
            PropertyHolder.AddChild(holderPerDocument);

            var toolManager = document.Get<ToolManager>();
            foreach (var tool in toolManager.Tools)
            {
                var container = new PropertyContainer();
                container.VisibleIf(toolManager.WorkingTool, tool);
                container.QueueFreeChildren();
                tool.DrawProperty(container);

                holderPerDocument.AddChild(container);
            }

            holderPerDocument.AddChild(new Label
                {
                    Text = "[Cannot Tool Layer]".Tr(),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                }
                .VisibleIf(toolManager.WorkingTool, (ITool)null));
        }).AddTo(this);
    }
}
