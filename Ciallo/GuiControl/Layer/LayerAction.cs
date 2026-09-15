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
        Root.ConvertToShape.VisibleIf(sm.PrimaryLayer,
            e => e.TryHas<VectorFillLayerSetting>() || e.TryHas<ImageLayerSetting>(), subs);
        LayerSelectionActions.ObservePrimary(sm, s => s.ClippingMask)
            .Subscribe(Root.ClippingMask.SetPressedNoSignal).AddTo(subs);
        Root.ClippingMask.OnToggledAsObservable().Subscribe(v => LayerSelectionActions.SetProperty(
            "Layer Clipping Mask", sm.SelectedLayers.Value, s => s.ClippingMask, v)).AddTo(subs);
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
        var primaryLayerE = Document.Get<SelectionManager>().PrimaryLayer.CurrentValue;
        LayerContextActions.NewShapeLayer(primaryLayerE.IsNull ? Document : primaryLayerE);
    }

    public void OnNewFolderLayer()
    {
        var primaryLayerE = Document.Get<SelectionManager>().PrimaryLayer.CurrentValue;
        LayerContextActions.NewFolderLayer(primaryLayerE.IsNull ? Document : primaryLayerE);
    }

    public void OnRemoveLayer()
    {
        LayerContextActions.DeleteLayers(Document.Get<SelectionManager>().SelectedLayers.Value);
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
            Document.Get<SelectionManager>().PrimaryLayer.CurrentValue);
    }

    /// <summary>
    /// Returns the parent entity and insertion index for a new layer based on the current primary layer.
    /// Folder primary layer → last child (visual top). Regular layer → sibling above. No selection → append to root.
    /// </summary>
    private (Entity parentE, int index) GetNewLayerInsertPosition()
    {
        var primaryLayerE = Document.Get<SelectionManager>().PrimaryLayer.CurrentValue;
        if (primaryLayerE.IsNull || primaryLayerE.IsDocument)
            return (AppDocumentManager.WorkingDocument.Value, -1);

        if (primaryLayerE.Has<FolderLayerSetting>())
            return (primaryLayerE, -1); // -1 resolves to Children.Count = last child = visual top

        // Regular layer: insert as sibling just above in screen (higher index in reversed display)
        var layerNode = primaryLayerE.Get<LayerTreeNode>();
        return (layerNode.ParentValue, layerNode.Index + 1);
    }
}
