using System;
using Ciallo.Data;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

[SceneTree]
public partial class ExportImage : ConfirmationDialog
{
    public readonly ReactiveProperty<float> Scale = new(1f);
    public readonly ReactiveProperty<Color> BackgroundColor = new(Colors.Transparent);

    private DocumentSetting _setting;
    private SubViewport _paintViewport;
    private CompositeDisposable _previewSubs = new();
    private readonly Polygon2D _background = new()
    {
        Name = "ExportBackground",
        VisibilityLayer = (uint)AppGodotLayers.Render2DLayer.View,
        ZIndex = -1,
    };

    public override void _Ready()
    {
        var subs = new CompositeDisposable();
        ScaleNumber.BindNumber(Scale, subs);
        BackgroundColorButton.BindColor(BackgroundColor, subs);
        subs.AddTo(this);

        ImageSubViewport.AddChild(_background);
        ImageSubViewport.MoveChild(_background, 0);
        Confirmed += OnExport;
    }

    private async void OnExport()
    {
        var filePath = PathPicker.Path.PathJoin(FileNameEdit.Text) + ".png";

        ConfigureExportViewport();
        ImageSubViewport.World2D = _paintViewport.FindWorld2D();

        try
        {
            GetOkButton().Disabled = true;
            Message.Hide();
            ImageSubViewport.RenderTargetClearMode = SubViewport.ClearMode.Once;
            ImageSubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            await ExportPngWriter.SaveAsync(ImageSubViewport, filePath);

            Message.Show();
        }
        catch (Exception exception)
        {
            GD.PrintErr(exception);
            AppDialogHost.WarnUser.DialogText = "Cannot export image.".Tr() + " " + exception.Message;
            AppDialogHost.WarnUser.Popup();
        }
        finally
        {
            GetOkButton().Disabled = false;
            ImageSubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        }
    }

    public void Init()
    {
        Message.Hide();
        _previewSubs.Dispose();
        _previewSubs = new();

        var document = AppDocumentManager.WorkingDocument.CurrentValue;
        _setting = document.Get<DocumentSetting>();
        _paintViewport = (SubViewport)document.Get<WorldView>().GetParent();

        BackgroundColor.Subscribe(c =>
        {
            _background.Color = c;
            ImageSubViewport.RenderTargetClearMode = SubViewport.ClearMode.Once;
            ImageSubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }).AddTo(_previewSubs);

        var rSize = _setting.ReferenceSize.Value;
        _background.SetPolygonFromRawRing(new Vector2[] { new(-rSize.X / 2, -rSize.Y / 2), new(rSize.X / 2, -rSize.Y / 2), new(rSize.X / 2, rSize.Y / 2), new(-rSize.X / 2, rSize.Y / 2) });
        ReferenceSizeNumber.Text = $"{rSize.X} x {rSize.Y}";
        Scale.Subscribe(s =>
        {
            var size = GetExportSize(rSize, s);
            FinalImageSizeNumber.Text = $"{size.X} x {size.Y}";
            ConfigureExportViewport();
            ImageSubViewport.RenderTargetClearMode = SubViewport.ClearMode.Once;
            ImageSubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }).AddTo(_previewSubs);

        ConfigureExportViewport();
        ImageSubViewport.World2D = _paintViewport.FindWorld2D();
        ImageSubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        ImageSubViewport.RenderTargetClearMode = SubViewport.ClearMode.Once;

        FileNameEdit.Text = _setting.Name.Value;
        PathPicker.Path = _setting.FilePath.Value.GetBaseDir();
    }

    private void ConfigureExportViewport()
    {
        ImageSubViewport.TransparentBg = true;
        // Match the main paint viewport so exported colors follow the user's visible composition.
        ImageSubViewport.UseHdr2D = true;
        ImageSubViewport.CanvasCullMask = (uint)AppGodotLayers.Render2DLayer.View;
        ImageSubViewport.Size = GetExportSize(_setting.ReferenceSize.Value, Scale.Value);
        Camera.Zoom = Vector2.One * Scale.Value;
    }

    private static Vector2I GetExportSize(Vector2 referenceSize, float scale)
    {
        return new Vector2I(
            Mathf.RoundToInt(referenceSize.X * scale),
            Mathf.RoundToInt(referenceSize.Y * scale));
    }
}
