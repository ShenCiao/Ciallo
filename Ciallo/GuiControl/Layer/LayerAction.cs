using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

[SceneTree(root: "Root"), Instantiable]
public partial class LayerAction : Control
{
    private Entity Document { get; set; }

    public void Init(Entity document)
    {
        Document = document;
        var sm = Document.Get<SelectionManager>();
        var subs = new CompositeDisposable();
        Root.ConvertToShape.VisibleIf(sm.WorkingLayer,
            e => e.TryHas<VectorFillLayerSetting>() || e.TryHas<ImageLayerSetting>(), subs);
        subs.AddTo(Document);
    }

    public override void _Ready()
    {
        Root.NewLayer.Pressed += OnNewShapeLayer;
        Root.NewFolder.Pressed += OnNewFolderLayer;
        Root.RemoveLayer.Pressed += OnRemoveLayer;
        Root.NewImage.Pressed += OnNewImage;
        Root.ConvertToShape.Pressed += OnConvertToShape;
        Root.FileDialog.FileSelected += OnImageFileSelected;
    }

    public void OnNewShapeLayer()
    {
        var workingLayerE = Document.Get<SelectionManager>().WorkingLayer.Value;
        LayerContextActions.NewShapeLayer(workingLayerE.IsNull ? Document : workingLayerE);
    }

    public void OnNewFolderLayer()
    {
        var (parentE, index) = GetNewLayerInsertPosition();
        new CommandBuilder("New Folder Layer", Document.World.Create())
            .NewFolderLayer()
            .AddToLayerTree(parentE, index)
            .SetWorkingLayer()
            .Commit();
    }

    public void OnRemoveLayer()
    {
        var document = Document;
        var currentLayerE = document.Get<SelectionManager>().WorkingLayer.Value;
        if (currentLayerE.IsNull) return;

        var workingLayerE = document.Get<SelectionManager>().WorkingLayer.Value;
        var root = document.Get<LayerTreeNode>();
        var workingLayerPath = root.FindPathTo(workingLayerE);
        var nextLayerPath = root.GetNextFocusPathAfterDeletion(workingLayerPath);
        var nextLayerE = nextLayerPath.IsEmpty ? document : root.GetDescendant(nextLayerPath);

        var cmd = new CommandBuilder("Delete Layer", nextLayerE)
            .SetWorkingLayer();

        if (currentLayerE.Tagged<CelTag>())
        {
            var celFolderE = currentLayerE.Get<LayerTreeNode>().ParentValue;
            cmd.SetTarget(celFolderE)
                .SetObservableCollection(
                    e => e.Get<FolderLayerSetting>().Exposures,
                    exposures =>
                    {
                        foreach (var (frame, celE) in exposures.ToArray())
                        {
                            if (celE == currentLayerE)
                                exposures.Remove(frame);
                        }
                    });
        }

        cmd.SetTarget(currentLayerE)
            .RemoveFromLayerTree()
            .DeleteLayer()
            .Commit();
    }

    public void OnNewImage()
    {
        if (AppDocumentManager.WorkingDocument.Value.IsNull) return;
        Root.FileDialog.Popup();
    }

    public void OnImageFileSelected(string path)
    {
        Image image;
        try
        {
            image = Image.LoadFromFile(path);
        }
        catch (System.Exception e)
        {
            GD.PushError($"Failed to load image from '{path}': {e.Message}");
            return;
        }
        if (image == null || image.IsEmpty())
        {
            GD.PushError($"Loaded image from '{path}' is empty.");
            return;
        }
        var (parentE, index) = GetNewLayerInsertPosition();
        new CommandBuilder("New Image Layer", Document.World.Create())
            .NewImageLayer(image)
            .AddToLayerTree(parentE, index)
            .Commit();
    }

    public void OnConvertToShape()
    {
        LayerConversionActions.ConvertToShape(
            Document.Get<SelectionManager>().WorkingLayer.Value);
    }

    /// <summary>
    /// Returns the parent entity and insertion index for a new layer based on the current working layer.
    /// Folder working layer → last child (visual top). Regular layer → sibling above. No selection → append to root.
    /// </summary>
    private (Entity parentE, int index) GetNewLayerInsertPosition()
    {
        var workingLayerE = Document.Get<SelectionManager>().WorkingLayer.Value;
        if (workingLayerE.IsNull || workingLayerE.IsDocument)
            return (AppDocumentManager.WorkingDocument.Value, -1);

        if (workingLayerE.Has<FolderLayerSetting>())
            return (workingLayerE, -1); // -1 resolves to Children.Count = last child = visual top

        // Regular layer: insert as sibling just above in screen (higher index in reversed display)
        var layerNode = workingLayerE.Get<LayerTreeNode>();
        return (layerNode.ParentValue, layerNode.Index + 1);
    }
}
