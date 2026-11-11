using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Ciallo.Widget;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PaintStrokeHover : Interaction, IPropertyProvider
{
    [StateAccess]
    public PaintStrokeTool Tool { get; set; }

    private MultiMeshInstance2D _snapDots;

    public override void Start(CursorButtonData data)
    {
        Document.Get<WorldBody>().DefaultCursorShape = Control.CursorShape.Cross;
        _snapDots = AutoloadRendering.CreateDots();
        Document.Get<WorldOverlay>().AddChild(_snapDots);
        RefreshSnap(data.WorldPosition);
    }
    public override void Moving(CursorMotionData data)
    {
        RefreshSnap(data.WorldPosition);
    }
    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel()
    {
        Document.Get<WorldBody>().DefaultCursorShape = default;
        _snapDots?.QueueFree();
        _snapDots = null;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        // ---- Mode selector
        var modeButton = new OptionButton
        {
            CustomMinimumSize = new(0, 32),
        };
        modeButton.AddItem("Freehand");
        modeButton.AddItem("Bezier");
        modeButton.AddItem("Poly Cubic Bézier");
        modeButton.BindSelectionIndex(Tool.Mode);
        container.AddProperty("Mode", modeButton);

        // ---- Document brush library
        var brushPreview = StrokeBrushPreviewList.New(Document);
        brushPreview.CustomMinimumSize = new(0, 256);
        container.AddChild(brushPreview);

        container.AddProperty("Radius", StrokeBrushToolControls.CreateRadius(Document));
        container.AddProperty("Color", StrokeBrushToolControls.CreateColor(Document));
    }

    internal void RefreshSnapping() => RefreshSnap(LatestCursor.WorldPosition);

    private void RefreshSnap(Vector2 worldPosition)
    {
        var target = Tool.TryFindSnapTarget(worldPosition);
        if (!target.HasValue)
        {
            _snapDots.Visible = false;
            return;
        }

        _snapDots.Visible = true;
        _snapDots.SetDotGeometry([target.Value.HitPoint], AppPreference.StrokeDotRadius);
    }
}
