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

            var tree = InteractionPropertyTree.Build(
                InteractionManager.StateMachine,
                InteractionManager.Global,
                document);
            holderPerDocument.AddChild(tree.RootControl);

            var cannotToolLabel = new Label
            {
                Text = "[Cannot Tool Layer]".Tr(),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            holderPerDocument.AddChild(cannotToolLabel);

            void Refresh()
            {
                tree.RefreshVisibility();
                cannotToolLabel.Visible = InteractionManager.StateMachine.State is
                    GlobalInteractiveScope or NoDocument or TimelineRolling;
            }

            InteractionManager.StateChanged += Refresh;
            holderPerDocument.TreeExiting += () => InteractionManager.StateChanged -= Refresh;
            Refresh();
        }).AddTo(this);
    }
}
