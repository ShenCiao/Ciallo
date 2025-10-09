using System.Collections.Specialized;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Misc;
using Ciallo.NodeControl;
using Ciallo.Tool;
using Ciallo.Widget;
using Godot;
using Massive;
using ObservableCollections;
using R3;

public partial class PaintTool : CommonToolBase
{
    public readonly ReactiveProperty<Entity> BrushE = new(new Entity());
    
    public override InteractorBase LeftInteractor => PaintInteractor;
    
    public readonly PaintInteractor PaintInteractor = new();
    // Will have dual interactors
    // public readonly ResizeBrushInteractor ResizeInteractor = new();

    public override void _Ready()
    {
        base._Ready();
        SetPressed(true);

        RegisterDocumentBrushPanel();
    }

    // Refactor: Move to another place when writing deserialization logic.
    private static void RegisterDocumentBrushPanel()
    {
        AppWorldManager.LoadedWorlds.ObserveChanged().Subscribe(e =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    var docAdd = e.NewItem.Document();
                    var panel = BrushPanel.Instantiate();
                    panel.Title = "Brush in document";
                    panel.Visible = false;
                    panel.PopupWindow = true; // Hint user this is different from the brush library panel
                    panel.Exclusive = false; // Allow propagating input (redo/undo mainly) to main window
                    docAdd.Set(panel);
                    ((SceneTree)Engine.GetMainLoop()).GetCurrentScene().AddChild(panel);

                    panel.BrushPreviewContainer.Visible = false; // Add preview someday
                    var bm = docAdd.Get<BrushManager>();
                    panel.BindBrushSetting(bm.Brushes, ent => ent.Get<BrushSetting>());

                    panel.Operators.Visible = false;
                    break;
                case NotifyCollectionChangedAction.Remove:
                    var docRemove = e.OldItem.Document();
                    docRemove.Get<BrushPanel>().QueueFree();
                    docRemove.Remove<BrushPanel>();
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var world in AppWorldManager.LoadedWorlds)
                    {
                        var doc = world.Document();
                        doc.Get<BrushPanel>().QueueFree();
                        doc.Remove<BrushPanel>();
                    }
                    break;
                default: throw new("unreachable");
            }
        });
    }

    public override void DrawProperty(PropertyContainer container, Entity document)
    {
        var brushSelector = new OptionButton();
        brushSelector.ObserveObservableList(AppBrushLibrary.BrushSettings, s => s.Name);
        brushSelector.BindSelectionIndex(AppBrushLibrary.SelectedIndex);
        container.AddProperty("Library brush", brushSelector);
        
        var appBrushRadiusControl = new SpinSlider()
        {
            MinValue = 0.1f,
            MaxValue = 256f,
            Step = 0.03333333f,
            ExpEdit = true
        };
        var boxBrushRadius = container.AddProperty("Radius", appBrushRadiusControl);
        boxBrushRadius.VisibleIf(AppBrushLibrary.SelectedIndex, v => v >= 0);
        var radiusView = AppBrushLibrary.SelectedIndex
            .Select(idx => AppBrushLibrary.BrushSettings.ElementAtOrDefault(idx)?.BaseRadius).ToReadOnlyReactiveProperty();
        appBrushRadiusControl.ReactiveBindNumber(radiusView);
        
        var appBrushColorControl = new ColorPickerButton()
        {
            CustomMinimumSize = new(0, 30),
        };
        var boxBrushColor = container.AddProperty("Color", appBrushColorControl);
                boxBrushColor.VisibleIf(AppBrushLibrary.SelectedIndex, v => v >= 0);
        var colorView = AppBrushLibrary.SelectedIndex
            .Select(idx => AppBrushLibrary.BrushSettings.ElementAtOrDefault(idx)?.Color ).ToReadOnlyReactiveProperty();
        appBrushColorControl.ReactiveBindColor(colorView);
        
        var useBrushButton = new Button()
        {
            Text = "Use brush",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new(0, 30),
            SizeFlagsHorizontal = SizeFlags.Fill
        };
        useBrushButton.VisibleIf(AppBrushLibrary.SelectedIndex, v => v >= 0);
        useBrushButton.Pressed += OnUseBrushPressed;
        var manageButton = new Button()
        {
            Text = "Manage brush library",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new(0, 30),
            SizeFlagsHorizontal = SizeFlags.Fill
        };
        manageButton.Pressed += () => GetTree().GetNodesInGroup("Dialog").OfType<BrushPanel>().First().Popup();
        var box = new VBoxContainer()
        {
            SizeFlagsHorizontal = SizeFlags.Fill
        };
        box.AddChild(manageButton);
        box.AddChild(useBrushButton);
        container.AddChild(box);
        // ---------------------------------------------
        var brushList = new DocumentBrushList()
        {
            CustomMinimumSize = new(0, 200),
        };
        brushList.ItemSelected += idx =>
        {
            new ChangeWorkingBrushCmd((int)idx).Commit();
        };
        var brushM = document.Get<BrushManager>();
        var selectionM = document.Get<SelectionManager>();
        foreach(var brushE in brushM.Brushes)
            brushList.AddItem(brushE.Get<BrushSetting>().Name.Value);
        document.Set(brushList);
        container.AddProperty("Brush in document", brushList);
        
        var radiusControl = new SpinSlider
        {
            MinValue = 0.1f,
            MaxValue = 256f,
            Step = 0.03333333f,
            ExpEdit = true,
        };
        var radiusBox = container.AddProperty("Radius", radiusControl);
        radiusBox.VisibleIf(selectionM.WorkingBrush, e => e.IsNotNull());
        var rView = selectionM.WorkingBrush
            .Select(e => e.IsNull() ? null : e.Get<BrushSetting>().BaseRadius).ToReadOnlyReactiveProperty();
        radiusControl.ReactiveBindNumber(rView);
        
        var manageDocumentBrush = new Button()
        {
            Text = "Manage brush in document",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new(0, 30),
            SizeFlagsHorizontal = SizeFlags.Fill
        };
        manageDocumentBrush.Pressed += () => document.Get<BrushPanel>().Popup();
        container.AddChild(manageDocumentBrush);
    }
    
    private void OnUseBrushPressed()
    {
        if (!AppBrushLibrary.HasSelection) return;
        var setting = AppBrushLibrary.SelectedBrushSetting.CurrentValue;
        new NewBrushCmd(setting).Combine(new ChangeWorkingBrushCmd(^1)).Commit();
    }
}

public partial class DocumentBrushList : ItemList;