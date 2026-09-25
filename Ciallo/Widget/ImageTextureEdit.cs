using System;
using Ciallo.Data;
using Godot;
using R3;

namespace Ciallo.Widget;

[SceneTree]
public partial class ImageTextureEdit : BoxContainer
{
    public Action<Image> ImageProcess;
    public ReactiveProperty<ImageTexture> Texture;

    [OnInstantiate]
    public void Initialise(ReactiveProperty<ImageTexture> texture, Action<Image> imageProcessOnLoad = null)
    {
        Texture = texture;
        ImageProcess = imageProcessOnLoad;
    }

    public override void _Ready()
    {
        Texture.Subscribe(TexturePreview.SetTexture);

        LoadButton.Pressed += () => FileDialog.Popup();
        ClearButton.Pressed += () =>
        {
            Texture.Value = null;
        };

        RotateButton.Pressed += () =>
        {
            var image = Texture.Value.GetImage();
            image.Rotate90(ClockDirection.Clockwise);
            if (!image.HasMipmaps()) image.GenerateMipmaps();

            Texture.Value = ImageTexture.CreateFromImage(image);
        };

        InvertColorButton.Pressed += () =>
        {
            var image = Texture.Value.GetImage();
            image.ClearMipmaps();
            var w = image.GetWidth();
            var h = image.GetHeight();
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                image.SetPixel(x, y, image.GetPixel(x, y).Inverted());
            }
            image.GenerateMipmaps();

            Texture.Value = ImageTexture.CreateFromImage(image);
        };

        FileDialog.FileSelected += path =>
        {
            var image = Image.LoadFromFile(path);
            if (image == null || image.IsEmpty())
            {
                AppDialogHost.WarnUser.DialogText = "[Cannot Load Image]";
                AppDialogHost.WarnUser.Popup();
                return;
            }
            ImageProcess?.Invoke(image);
            image.GenerateMipmaps();

            Texture.Value = ImageTexture.CreateFromImage(image);
        };
    }
}
