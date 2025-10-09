using Massive;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.NodeControl;

public partial class ExportGodotScene : FileDialog
{
    public override void _Ready()
    {
        FileSelected += filePath =>
        {
            var world = AppWorldManager.WorkingWorld.Value;
            var document = world.Document();
            var worldView = document.Get<WorldView>();
            var rawScene = new PackedScene();
            // Duplicate
            rawScene.Pack(worldView);
            var oldRoot = rawScene.Instantiate<Node>();
            var root = new Node2D();
            oldRoot.ReplaceBy(root);
            //// In ideal godot, we should do this:
            // oldRoot.SetScript(new());
            //// But this gives "Cannot access a disposed object." error for unknown reason.

            foreach (var child in root.GetAllDescendants())
            {
                // Remove unnecessary canvas group
                if (child is PolylineLayerView { IsDefault: true } layer)
                {
                    var node = new Node2D();
                    layer.ReplaceBy(node);
                    node.SetOwner(root);
                    layer.QueueFree();
                }
                
                // Remove script
                if (child is StrokeView stroke)
                {
                    if (stroke.Material.GetScript().VariantType != Variant.Type.Nil)
                        stroke.Material.SetScript(new());
                }

                child.SetScript(new());
            }

            root.Name = filePath.GetFile().GetBaseName();
            var outputView = new PackedScene();
            outputView.Pack(root);
            ResourceSaver.Save(outputView, filePath);
            root.QueueFree();
            oldRoot.QueueFree();
        };
    }
}
