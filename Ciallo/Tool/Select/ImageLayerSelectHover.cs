using System;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class ImageLayerSelectHover : Interaction
{
    public Body RotationBody;
    public Body TranslationBody;
    public Body[] CornerBodies = [];

    public override void Start(CursorButtonData data)
    {
        var setting = PrimaryLayer.Get<ImageLayerSetting>();
        var worldBody = Document.Get<WorldBody>();

        worldBody.EnableHoverDetection = true;
        worldBody.CursorWorldPosition = data.WorldPosition;
        PrimaryLayer.Get<TransformOverlayBox>().Visible = true;

        // Create bodies
        Body[] bodies = worldBody.CreateAddTransformAreas(setting.ImageSize, setting.ImageTransform.Value);
        RotationBody = bodies[0];
        TranslationBody = bodies[1];
        CornerBodies = bodies[2..6];
    }

    public override void Moving(CursorMotionData data)
    {
        Document.Get<WorldBody>().CursorWorldPosition = data.WorldPosition;
    }

    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel()
    {
        RotationBody.QueueFree();
        TranslationBody.QueueFree();

        Array.ForEach(CornerBodies, b => b.QueueFree());
        RotationBody = null;
        TranslationBody = null;
        CornerBodies = [];

        PrimaryLayer.Get<TransformOverlayBox>().Visible = false;
        Document.Get<WorldBody>().EnableHoverDetection = false;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data)
    {
        return false;
    }
}