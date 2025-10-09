using Massive;
using Ciallo.Data;
using Ciallo.Misc;
using Ciallo.Rendering;
using Ciallo.Widget;
using Godot;
using R3;

public partial class ExportImage : ConfirmationDialog
{
    public readonly ReactiveProperty<float> Scale = new(1f);
    public readonly ReactiveProperty<Color> BackgroundColor = new(Colors.Transparent);

    public Label ReferenceSizeNumber;
    public SpinSlider ScaleNumber;
    public Label FinalImageSizeNumber;
    public SubViewport ImageSubViewport;
    public FilePathPicker PathPicker;
    public LineEdit FileNameEdit;
    public TextureRect ImageTextureRect;
    public Label Message;
    public ColorPickerButton BackgroundColorButton;
    
    private DocumentSetting _setting;
    private Camera2D _camera;
    private Polygon2D _background;

    public override void _Ready()
    {
        ReferenceSizeNumber = GetNode<Label>("%ReferenceSizeNumber");
        ScaleNumber = GetNode<SpinSlider>("%ScaleNumber");
        FinalImageSizeNumber = GetNode<Label>("%FinalImageSizeNumber");
        ImageSubViewport = GetNode<SubViewport>("%ImageSubViewport");
        PathPicker = GetNode<FilePathPicker>("%FilePathPicker");
        FileNameEdit = GetNode<LineEdit>("%FileNameEdit");
        ImageTextureRect = GetNode<TextureRect>("%ImageTextureRect");
        Message = GetNode<Label>("%Message");
        BackgroundColorButton = GetNode<ColorPickerButton>("%BackgroundColorButton");
        
        ScaleNumber.BindNumber(Scale);
        BackgroundColorButton.BindColor(BackgroundColor);
        Confirmed += OnExport;
    }

    private async void OnExport()
    {
        var filePath = PathPicker.Path.PathJoin(FileNameEdit.Text) + ".png";
        var imageSize = _setting.ReferenceSize.Value * Scale.Value;
        ImageSubViewport.Size = new((int)imageSize.X, (int)imageSize.Y);
        _camera.Zoom = Vector2.One * Scale.Value;
        
        ImageSubViewport.RenderTargetClearMode = SubViewport.ClearMode.Once;
        ImageSubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = ImageSubViewport.GetTexture().GetImage();
        image.SavePng(filePath);
        Message.Show();
    }

    public void Init()
    {
        ImageSubViewport.QueueFreeChildren();
        Message.Hide();
        
        var document = AppWorldManager.WorkingDocument.CurrentValue;
        _setting = document.Get<DocumentSetting>();
        var view = document.Get<WorldView>();
        
        // Duplicate view
        var scene = new PackedScene();
        scene.Pack(view);
        var root = scene.Instantiate();
        _background = new();
        ImageSubViewport.AddChild(_background);
        BackgroundColor.Subscribe(c =>
        {
            _background.Color = c;
            ImageSubViewport.RenderTargetClearMode = SubViewport.ClearMode.Once;
            ImageSubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        });
        ImageSubViewport.AddChild(root);
        _camera = new Camera2D();
        ImageSubViewport.AddChild(_camera);

        // Size
        var rSize = _setting.ReferenceSize.Value;
        _background.Polygon = [ new(-rSize.X/2, -rSize.Y/2), new(rSize.X/2, -rSize.Y/2), new(rSize.X/2, rSize.Y/2), new(-rSize.X/2, rSize.Y/2) ];
        ReferenceSizeNumber.Text = $"{rSize.X} x {rSize.Y}";
        Vector2I sizei = new((int)rSize.X, (int)rSize.Y);
        Scale.Subscribe(s => 
        {
            Vector2I size = new((int)(rSize.X * s), (int)(rSize.Y * s));
            FinalImageSizeNumber.Text = $"{size.X} x {size.Y}";
        });
        
        ImageSubViewport.Size = sizei;
        ImageSubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        ImageSubViewport.RenderTargetClearMode = SubViewport.ClearMode.Once;
        
        // Path, filename
        FileNameEdit.Text = _setting.Name.Value;
        PathPicker.Path = _setting.FilePath.Value.GetBaseDir();
    }
}
