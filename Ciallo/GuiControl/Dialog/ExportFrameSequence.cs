using System;
using System.IO;
using System.Threading.Tasks;
using Ciallo.Data;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

public class FrameFileNameSetting
{
    public ReactiveProperty<string> Prefix = new("frame");
    public ReactiveProperty<string> Suffix = new("");
    public ReactiveProperty<string> Separator = new("_");
    public ReactiveProperty<int> StartNumber = new(0);
    public int NumberDigits = 4; // Not show user in current version. 
}

[SceneTree]
public partial class ExportFrameSequence : ConfirmationDialog
{
    public readonly ReactiveProperty<float> Scale = new(1f);
    public readonly ReactiveProperty<Color?> BackgroundColor = new(default); // Use nullable color button
    public readonly ReactiveProperty<string> ExportPath = new("");
    public FrameFileNameSetting NameSetting = new();
    public ReadOnlyReactiveProperty<string> PreviewFrameFileName;

    public Entity Document;
    private CompositeDisposable _popupSubs = new();
    private readonly Polygon2D _background = new()
    {
        Name = "ExportBackground",
        VisibilityLayer = (uint)AppGodotLayers.Render2DLayer.View,
        ZIndex = -1,
        Visible = false,
    };

    public ExportFrameSequence()
    {
        PreviewFrameFileName = NameSetting.Prefix.CombineLatest(NameSetting.Suffix, NameSetting.Separator, NameSetting.StartNumber,
            (prefix, suffix, separator, startNumber) => FormatFrameFileName(prefix, suffix, separator, startNumber, NameSetting.NumberDigits))
            .ToReadOnlyReactiveProperty();
    }

    public override void _Ready()
    {
        var subs = new CompositeDisposable();

        ScaleNumber.BindNumber(Scale, subs);
        BackgroundColorButton.BindColor(BackgroundColor, subs);
        BindExportPath(subs);
        PrefixEdit.BindString(NameSetting.Prefix, subs);
        SuffixEdit.BindString(NameSetting.Suffix, subs);
        SeparatorEdit.BindString(NameSetting.Separator, subs);
        StartNumber.BindNumber(NameSetting.StartNumber);
        PreviewFrameFileName.Subscribe(fileName => PreviewFrameFileNameLabel.Text = fileName).AddTo(subs);
        subs.AddTo(this);

        ExportViewport.AddChild(_background);
        ExportViewport.MoveChild(_background, 0);
        Confirmed += Export;
    }

    private async void Export()
    {
        GetOkButton().Disabled = true;
        var progressBarPopup = GetNode<PopupPanel>("ProgressBarPopup");
        progressBarPopup.PopupCentered();
        try
        {
            await ExportFrames();
            Hide();
        }
        catch (Exception exception)
        {
            GD.PrintErr(exception);
            AppDialogHost.WarnUser.DialogText = "Cannot export image.".Tr() + " " + exception.Message;
            AppDialogHost.WarnUser.Popup();
        }
        finally
        {
            progressBarPopup.Hide();
            GetOkButton().Disabled = false;
        }
    }

    private async Task ExportFrames()
    {
        var paintViewport = (SubViewport)Document.Get<WorldView>().GetParent();
        var documentSetting = Document.Get<DocumentSetting>();
        var timelineSetting = Document.Get<TimelineSetting>();
        var selectionManager = Document.Get<SelectionManager>();

        var oldFrame = selectionManager.CurrentFrame.Value;
        var oldPaintViewportCullMask = paintViewport.CanvasCullMask;
        var oldExportWorld = ExportViewport.World2D;

        Directory.CreateDirectory(ExportPath.Value);

        var referenceSize = documentSetting.ReferenceSize.Value;
        ConfigureExportViewport(referenceSize);
        ConfigureExportBackground(referenceSize);

        var startFrame = timelineSetting.PlaybackStart.Value;
        var endFrameExclusive = timelineSetting.PlaybackEnd.Value;
        var frameCount = endFrameExclusive - startFrame;
        var progressBar = GetNode<Godot.ProgressBar>("ProgressBarPopup/ProgressBar");
        progressBar.MinValue = 0;
        progressBar.MaxValue = frameCount;
        progressBar.Value = 0;

        ExportViewport.World2D = paintViewport.FindWorld2D();
        paintViewport.CanvasCullMask = 0;
        timelineSetting.IsRollingFrame.Value = true;

        try
        {
            // image2 interprets percent signs in the complete path as patterns.
            var escapedDirectory = ExportPath.Value.Replace("%", "%%");
            var outputPattern = escapedDirectory.PathJoin(FormatFrameFileName(
                NameSetting.Prefix.Value.Replace("%", "%%"),
                NameSetting.Suffix.Value.Replace("%", "%%"),
                NameSetting.Separator.Value.Replace("%", "%%"),
                $"%0{NameSetting.NumberDigits}d"));
            await ExportPngWriter.SaveFramesAsync(ExportViewport.Size.X, ExportViewport.Size.Y,
                outputPattern, frameCount, NameSetting.StartNumber.Value, async index =>
            {
                selectionManager.CurrentFrame.Value = startFrame + index;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                ExportViewport.RenderTargetClearMode = SubViewport.ClearMode.Once;
                ExportViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

                var pixels = ExportPngWriter.ReadHdrPixels(ExportViewport);
                progressBar.Value = index + 1;
                return pixels;
            }, ExportPngWriter.GetBinaryDirectory());
        }
        finally
        {
            selectionManager.CurrentFrame.Value = oldFrame;
            paintViewport.CanvasCullMask = oldPaintViewportCullMask;
            _background.Visible = false;
            ExportViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            ExportViewport.World2D = oldExportWorld;
            timelineSetting.IsRollingFrame.Value = false;
        }
    }

    private void ConfigureExportViewport(Vector2 referenceSize)
    {
        ExportViewport.TransparentBg = true;
        // Match the main paint viewport so exported colors follow the user's visible composition.
        ExportViewport.UseHdr2D = true;
        ExportViewport.CanvasCullMask = (uint)AppGodotLayers.Render2DLayer.View;
        ExportViewport.Size = GetExportSize(referenceSize, Scale.Value);
        Camera.Zoom = Vector2.One * Scale.Value;
    }

    private static Vector2I GetExportSize(Vector2 referenceSize, float scale)
    {
        return new Vector2I(
            Mathf.RoundToInt(referenceSize.X * scale),
            Mathf.RoundToInt(referenceSize.Y * scale));
    }

    private void ConfigureExportBackground(Vector2 referenceSize)
    {
        _background.SetPolygonFromRawRing(new Vector2[]
        {
            new(-referenceSize.X / 2, -referenceSize.Y / 2),
            new(referenceSize.X / 2, -referenceSize.Y / 2),
            new(referenceSize.X / 2, referenceSize.Y / 2),
            new(-referenceSize.X / 2, referenceSize.Y / 2)
        });

        _background.Visible = BackgroundColor.Value.HasValue;
        if (BackgroundColor.Value.HasValue)
            _background.Color = BackgroundColor.Value.Value;
    }

    public void Popup(Entity document)
    {
        Document = document;
        var documentSetting = document.Get<DocumentSetting>();
        var referenceSize = documentSetting.ReferenceSize.Value;

        _popupSubs.Dispose();
        _popupSubs = new();
        ReferenceSizeNumber.Text = $"{referenceSize.X} x {referenceSize.Y}";
        Scale.Subscribe(s =>
        {
            var size = GetExportSize(referenceSize, s);
            FinalImageSizeNumber.Text = $"{size.X} x {size.Y}";
        }).AddTo(_popupSubs);

        ExportPath.Value = documentSetting.FilePath.Value.GetBaseDir();
        base.Popup();
    }

    private void BindExportPath(CompositeDisposable subs)
    {
        ExportPath.Subscribe(path => ExportPathPicker.Path = path).AddTo(subs);
        ExportPathPicker.PathEdit.OnTextChangedAsObservable()
            .Subscribe(path => ExportPath.Value = path)
            .AddTo(subs);
    }

    private static string FormatFrameFileName(string prefix, string suffix, string separator, int frameNumber, int digits)
        => FormatFrameFileName(prefix, suffix, separator, frameNumber.ToString($"D{digits}"));

    private static string FormatFrameFileName(string prefix, string suffix, string separator, string number)
    {
        // If prefix/suffix is not empty, add separator between prefix/suffix and number. If empty, not add separator.
        var prefixPart = string.IsNullOrEmpty(prefix) ? "" : $"{prefix}{separator}";
        var suffixPart = string.IsNullOrEmpty(suffix) ? "" : $"{separator}{suffix}";
        return $"{prefixPart}{number}{suffixPart}.png";
    }
}
