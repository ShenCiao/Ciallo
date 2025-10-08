using System.IO;
using Godot;

namespace Ciallo.Widget;

[Tool, GlobalClass]
public partial class FilePathPicker : HBoxContainer
{
    public LineEdit PathEdit;
    public Button OpenExplorerButton;
    public FileDialog FileDialog;
    
    public string Path
    {
        get => IsInstanceValid(PathEdit) ? PathEdit.Text : string.Empty;
        set
        {
            if (IsInstanceValid(PathEdit)) PathEdit.Text = value;
        }
    }


    [Export]
    public FileDialog.FileModeEnum FileMode = FileDialog.FileModeEnum.OpenFile;
    
    [Export(PropertyHint.Enum, "None:-1,Desktop:0,Dcim:1,Documents:2,Downloads:3")] // From OS.SystemDir
    public int DefaultPath = -1;

    public override void _Ready()
    {
        OpenExplorerButton = new Button
        {
            Icon = GD.Load<Texture2D>("res://Icon/folder-edit-outline.svg"),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            TooltipText = $"Select a {(FileMode == FileDialog.FileModeEnum.OpenDir? "folder":"file")}",
            ExpandIcon = true,
            CustomMinimumSize = new Vector2(32, 32),
        };
        PathEdit = new LineEdit
        {
            CustomMinimumSize = new Vector2(200, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        if(DefaultPath != -1)
            PathEdit.Text = OS.GetSystemDir((OS.SystemDir)DefaultPath) ?? string.Empty;
        
        AddChild(OpenExplorerButton);
        AddChild(PathEdit);
        OpenExplorerButton.Pressed += OnOpenExplorer;
    }

    private void OnOpenExplorer()
    {
        if (IsInstanceValid(FileDialog))
        {
            FileDialog.PopupCentered();
            return;
        }

        string path = PathEdit.Text ?? string.Empty;
        if (File.Exists(path) || Directory.Exists(path.GetBaseDir()))
        {
            path = path.GetBaseDir();
        }
        else
        {
            path = OS.GetSystemDir(OS.SystemDir.Desktop);
        }
        
        FileDialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileMode,
            CurrentDir = path,
            Size = new Vector2I(800, 600),
            Title = $"Select a {(FileMode == FileDialog.FileModeEnum.OpenDir? "folder":"file")}",
            Unresizable = false,
            DialogCloseOnEscape = true,
            UseNativeDialog = true,
        };
        if (FileMode == FileDialog.FileModeEnum.OpenDir)
        {
            FileDialog.DirSelected += OnSelected;
        }
        if(FileMode == FileDialog.FileModeEnum.OpenFile)
        {
            FileDialog.FileSelected += OnSelected;
        }
        AddChild(FileDialog);
        FileDialog.SetOwner(this);
        FileDialog.PopupCentered();
    }
    
    private void OnSelected(string path)
    {
        PathEdit.Text = path;
    }
}