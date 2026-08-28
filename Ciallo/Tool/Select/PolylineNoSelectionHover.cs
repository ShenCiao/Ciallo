using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

[RegisterState]
public class PolylineNoSelectionHover : Interaction
{
    public Entity CurrHoveredShape;

    public bool CanTranslate => !CurrHoveredShape.IsNull;

    protected CompositeDisposable Subs;

    public override void Start(CursorButtonData data)
    {
        Subs = new();
        var worldBody = Document.Get<WorldBody>();
        var layerBody = WorkingLayer.Get<BodyHolder>();

        // Enable cursor detections on shapes of working layer
        worldBody.EnableHoverDetection = true;
        worldBody.CursorWorldPosition = data.WorldPosition;
        layerBody.SetChildrenBodyCursor(Control.CursorShape.Move);

        // --- hover hinter
        Document.Get<WorldBody>().HoveringBody.Subscribe(ToggleWireframe).AddTo(Subs);
    }

    public void ToggleWireframe(Body body)
    {
        if (!CurrHoveredShape.IsDyingOrDead) CurrHoveredShape.Get<PolylineWireframe>().SetVisible(false);
        if (body == null)
        {
            CurrHoveredShape = Entity.Null;
            return;
        }
        if (!body.SelfEntity.IsDyingOrDead)
            body.SelfEntity.Get<PolylineWireframe>().SetVisible(true);
        CurrHoveredShape = body.SelfEntity;
    }

    public override void Moving(CursorMotionData data)
    {
        Document.Get<WorldBody>().CursorWorldPosition = data.WorldPosition;
    }

    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel()
    {
        Subs.Dispose();
        WorkingLayer.Get<BodyHolder>().SetChildrenBodyCursor(Control.CursorShape.Arrow);
        Document.Get<WorldBody>().EnableHoverDetection = false;

        // overlays
        if (!CurrHoveredShape.IsDyingOrDead)
            CurrHoveredShape.Get<PolylineWireframe>().SetVisible(false);

        CurrHoveredShape = Entity.Null;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data)
    {
        if (AppHotkeys.Global.EditCopy.IsPressedBy(key))
        {
            AppClipboardManager.CopyShapes(Document.Get<SelectionManager>().SelectedShapes);
            return true;
        }

        if (AppHotkeys.Global.EditCut.IsPressedBy(key))
        {
            var selectedShapes = Document.Get<SelectionManager>().SelectedShapes.ToArray();
            AppClipboardManager.CopyShapes(selectedShapes);
            DeleteShapes(selectedShapes);
            Fire(Trigger.Refresh);
            return true;
        }

        if (AppHotkeys.Global.EditPaste.IsPressedBy(key))
        {
            var pastedShapes = AppClipboardManager.PasteShapes(WorkingLayer);
            var selectedShapes = Document.Get<SelectionManager>().SelectedShapes;
            selectedShapes.Clear();
            selectedShapes.AddRange(pastedShapes);
            Fire(Trigger.Refresh);
            return true;
        }

        if (AppHotkeys.Global.InteractionCancel.IsPressedBy(key))
        {
            Document.Get<SelectionManager>().SelectedShapes.Clear();
            Fire(Trigger.Refresh);
            return true;
        }

        if (AppHotkeys.Global.EditDelete.IsPressedBy(key))
        {
            DeleteShapes(Document.Get<SelectionManager>().SelectedShapes.ToArray());
            Fire(Trigger.Refresh);
            return true;
        }

        if (key.IsPressed() && key.Keycode == Key.Shift)
        {
            WorkingLayer.Get<BodyHolder>().SetChildrenBodyCursor(Control.CursorShape.Arrow);
            Document.Get<WorldBody>().ForceUpdateCursor();
        }

        if (key.IsReleased() && key.Keycode == Key.Shift)
        {
            WorkingLayer.Get<BodyHolder>().SetChildrenBodyCursor(Control.CursorShape.Move);
            Document.Get<WorldBody>().ForceUpdateCursor();
        }

        return false;
    }

    private static void DeleteShapes(Entity[] shapeEs)
    {
        var cmd = new CommandBuilder("Delete Shapes");
        foreach (var e in shapeEs)
        {
            cmd.SetTarget(e).RemoveFromLayerTree().DeleteShape();
        }
        cmd.Commit();
    }

}
