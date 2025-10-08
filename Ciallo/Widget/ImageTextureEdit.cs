using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Ciallo.Data;
using Ciallo.Misc;
using Godot;

namespace Ciallo.Widget;

public partial class ImageTextureEdit : BoxContainer
{
    public TextureRect TexturePreview;
    public Button LoadButton;
    public Button ClearButton;
    public Button RotateButton;
    public Button InvertColorButton;
    public FileDialog FileDialog;
    public Action<Image> ImageProcess;
    public ImageTexture Texture;


    [OnInstantiate]
    public void Initialise([NotNull] ImageTexture texture, Action<Image> imageProcess = null)
    {
        Texture = texture;
        ImageProcess = imageProcess;
    }

    public override void _EnterTree()
    {
        TexturePreview = GetNode<TextureRect>("%TexturePreview");
        LoadButton = GetNode<Button>("%LoadButton");
        ClearButton = GetNode<Button>("%ClearButton");
        RotateButton = GetNode<Button>("%RotateButton");
        InvertColorButton = GetNode<Button>("%InvertColorButton");
        FileDialog = GetNode<FileDialog>("%FileDialog");
    }

    public override void _Ready()
    {
        TexturePreview.Texture = Texture;
        
        LoadButton.Pressed += () => FileDialog.PopupCentered();
        ClearButton.Pressed += () =>
        {
            Texture.SetImage(BrushSetting.CreateDefaultWhiteImage());
        };
        RotateButton.Pressed += () =>
        {
            var image = Texture.GetImage();
            image.Rotate90(ClockDirection.Clockwise);
            if(!image.HasMipmaps()) image.GenerateMipmaps();
            
            Texture.Update(image);
        };
        InvertColorButton.Pressed += () =>
        {
            var image = Texture.GetImage();
            image.ClearMipmaps();
            var w = image.GetWidth();
            var h = image.GetHeight();
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                image.SetPixel(x, y, image.GetPixel(x, y).Inverted());
            }
            image.GenerateMipmaps();
            
            Texture.Update(image);
        };
        
        FileDialog.FileSelected += path =>
        {
            var image = Image.LoadFromFile(path);
            if (image == null || image.IsEmpty())
            {
                var dialog = ((SceneTree)Engine.GetMainLoop()).GetNodesInGroup("Dialog").OfType<AcceptDialog>().Single(n => n.Name == "WarnUser");
                dialog.DialogText = "[Cannot Load Image]".Tr();
                dialog.Popup();
                return;
            }
            ImageProcess?.Invoke(image);
            image.GenerateMipmaps();
            Texture.SetImage(image);
        };
    }
}
