using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Frent.Components;
using Godot;
using R3;

namespace Ciallo.GuiControl;

public partial class DocumentBrushListViewer : ItemList, IInitable
{
    private Entity _document;
    public BrushManager Manager => _document.Get<BrushManager>();

    public DocumentBrushListViewer()
    {
        TooltipText = "[Document Brush List Tooltip]";
        AutoWidth = true;
    }

    public void Init(Entity document)
    {
        _document = document;

        ItemSelected += idx =>
        {
            new CommandBuilder("Set Working Stroke Brush", document.Get<BrushManager>().StrokeBrushes[(int)idx])
                .SetWorkingStrokeBrush()
                .Commit();
        };

        ItemClicked += async (idx, _, buttonIndex) =>
        {
            if ((MouseButton)buttonIndex != MouseButton.Right) return;
            var brushE = document.Get<BrushManager>().StrokeBrushes[(int)idx];
            var query = brushE.World.CreateQuery().With<StrokeSetting>().Tagged<ToSerializeTag>().Build();
            List<Entity> toDeleteShapes = [];
            foreach (var strokeE in query.EnumerateWithEntities())
            {
                if (strokeE.Get<StrokeSetting>().Brush.Value == brushE)
                    toDeleteShapes.Add(strokeE);
            }

            if (toDeleteShapes.Count > 0)
            {
                AppDialogHost.YesNoDialog.DialogText = "[Delete Brush Hint]";
                if (!await AppDialogHost.YesNoDialog.PopupCollectInput()) return;
            }

            var cmd = new CommandBuilder("Delete Stroke Brush", Entity.Null);
            foreach (var strokeE in toDeleteShapes)
            {
                cmd.SetTarget(strokeE)
                    .RemoveFromLayerTree()
                    .DeleteShape();
            }

            var selectionManager = document.Get<SelectionManager>();
            if (selectionManager.WorkingStrokeBrush.Value == brushE)
                cmd.SetTarget(Entity.Null).SetWorkingStrokeBrush();
            cmd.SetTarget(brushE).DeleteBrush().Commit();
        };

        var brushM = document.Get<BrushManager>();
        foreach (var brushE in brushM.StrokeBrushes)
            AddItem(brushE.Get<StrokeBrushSetting>().Name.Value);
    }

    public void Add(Entity brushE)
    {
        var setting = brushE.Get<StrokeBrushSetting>();
        AddItem(setting.Name.Value);
        var sub = setting.Name.Subscribe(s =>
        {
            var idx = Manager.StrokeBrushes.IndexOf(brushE);
            SetItemText(idx, s);
        });
        SetItemMetadata(ItemCount - 1, Callable.From(() => sub.Dispose()));
    }

    public void Remove(Entity brushE)
    {
        var idx = Manager.StrokeBrushes.IndexOf(brushE);
        var subDispose = (Callable)GetItemMetadata(idx);
        subDispose.Call();
        RemoveItem(idx);
    }
}
