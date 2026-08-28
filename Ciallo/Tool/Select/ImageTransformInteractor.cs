using Ciallo.Command;
using Ciallo.Data;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class ImageTransformInteractor : CapturingInteraction
{
    private int _transformType = -1; // 0: Rotate, 1: Move, 2~5: Corner Resize

    private ImageLayerSetting _setting;
    private Vector2 _startPos;
    private Transform2D _startTransform;
    private Vector2[] _startCorners = [];

    public override void BeforeSourceExit(Interaction session)
    {
        if (session is not ImageLayerSelectHover hover) return;
        if (hover.RotationBody.IsHovered)
        {
            _transformType = 0;
        }
        if (hover.TranslationBody.IsHovered)
        {
            _transformType = 1;
        }
        for (int i = 0; i < hover.CornerBodies.Length; i++)
        {
            if (hover.CornerBodies[i].IsHovered)
            {
                _transformType = 2 + i;
                break;
            }
        }
    }

    public override void Start(CursorButtonData data)
    {
        _setting = WorkingLayer.Get<ImageLayerSetting>();
        _startPos = data.WorldPosition;
        _startTransform = _setting.ImageTransform.Value;
        _startCorners = _setting.GetCorners();
    }

    public override void Moving(CursorMotionData data)
    {
        if (_transformType == 0)
        {
            var startAngle = (_startPos - _setting.Position).Angle();
            var currentAngle = (data.WorldPosition - _setting.Position).Angle();
            var deltaAngle = currentAngle - startAngle;
            _setting.ImageTransform.Value = _startTransform
                .Translated(-_startTransform.Origin)
                .Rotated(deltaAngle)
                .Translated(_startTransform.Origin);
        }

        if (_transformType == 1)
        {
            _setting.ImageTransform.Value = _startTransform.Translated(data.WorldPosition - _startPos);
        }

        if (_transformType >= 2) // gen by copilot
        {
            bool fixRatio = Input.IsKeyPressed(Key.Shift);
            bool fixCenter = Input.IsKeyPressed(Key.Alt); // Fix the center of transform

            var cornerIndex = _transformType - 2; // 0..3
            var oppositeIndex = (cornerIndex + 2) & 3; // (index + 2) % 4
            var fixedCorner = _startCorners[oppositeIndex];
            var draggedPos = data.WorldPosition;

            var origCenter = _startTransform.Origin;

            // Axis directions from original transform (normalized)
            var axisXDir = _startTransform.X.Normalized();
            var axisYDir = _startTransform.Y.Normalized();
            var origScaleXLen = _startTransform.X.Length();
            var origScaleYLen = _startTransform.Y.Length();

            // Sign of the dragged corner relative to center along each axis (used when anchoring opposite corner)
            var startOffsetCorner = _startCorners[cornerIndex] - origCenter;
            var signX = Mathf.Sign(startOffsetCorner.Dot(axisXDir));
            var signY = Mathf.Sign(startOffsetCorner.Dot(axisYDir));
            // let it crash philosophy normally, but zero would break orientation; treat 0 as positive
            if (signX == 0) signX = 1;
            if (signY == 0) signY = 1;

            // Local half extents in local space (image units)
            var startHalfLocal = _setting.ImageSize * 0.5f;

            Transform2D result;

            if (fixCenter)
            {
                // Scale about original center. Opposite corner is not fixed; center remains.
                var delta = draggedPos - origCenter;
                var newHalfXWorld = Mathf.Abs(delta.Dot(axisXDir));
                var newHalfYWorld = Mathf.Abs(delta.Dot(axisYDir));

                var newScaleXLength = newHalfXWorld / startHalfLocal.X;
                var newScaleYLength = newHalfYWorld / startHalfLocal.Y;

                if (fixRatio)
                {
                    // Uniform factor; take larger so the dragged distance isn't constrained inside the box.
                    var factorX = newScaleXLength / origScaleXLen;
                    var factorY = newScaleYLength / origScaleYLen;
                    var uniformFactor = Mathf.Max(factorX, factorY);
                    newScaleXLength = origScaleXLen * uniformFactor;
                    newScaleYLength = origScaleYLen * uniformFactor;
                }

                var newX = axisXDir * newScaleXLength;
                var newY = axisYDir * newScaleYLength;
                result = new Transform2D(newX, newY, origCenter);
            }
            else
            {
                // Anchor opposite corner (fixedCorner) similar to Illustrator standard behavior.
                // New center is midpoint initially (non-uniform) but recomputed for uniform scaling to keep anchor exact.
                var newCenter = (fixedCorner + draggedPos) * 0.5f;

                // Vector from center to dragged corner (half diagonal in world space)
                var halfDiag = draggedPos - newCenter;

                // Project half diagonal onto axes to get new half sizes in world along each axis
                var newHalfXWorld = Mathf.Abs(halfDiag.Dot(axisXDir));
                var newHalfYWorld = Mathf.Abs(halfDiag.Dot(axisYDir));

                var newScaleXLength = newHalfXWorld / startHalfLocal.X;
                var newScaleYLength = newHalfYWorld / startHalfLocal.Y;

                if (fixRatio)
                {
                    var factorX = newScaleXLength / origScaleXLen;
                    var factorY = newScaleYLength / origScaleYLen;
                    var uniformFactor = Mathf.Max(factorX, factorY);
                    newScaleXLength = origScaleXLen * uniformFactor;
                    newScaleYLength = origScaleYLen * uniformFactor;
                    // Recompute center so fixedCorner stays fixed with the new uniform scaling.
                    // Vector from center to dragged corner after uniform scaling:
                    var centerToDragged = axisXDir * (signX * startHalfLocal.X * newScaleXLength) +
                                          axisYDir * (signY * startHalfLocal.Y * newScaleYLength);
                    // fixedCorner is opposite corner: fixedCorner = newCenter - centerToDragged (since signs are opposite)
                    newCenter = fixedCorner + centerToDragged * 1.0f;
                    // because fixedCorner + (centerToDragged) = center + ... wait adjust below

                    // Actually: dragged corner = newCenter + centerToDragged; fixedCorner = newCenter - centerToDragged.
                    // So newCenter = (dragged + fixed)/2 = fixedCorner + centerToDragged.
                    // The above line already matches formula: newCenter = fixedCorner + centerToDragged.
                }

                var newX = axisXDir * newScaleXLength;
                var newY = axisYDir * newScaleYLength;
                result = new Transform2D(newX, newY, newCenter);
            }

            _setting.ImageTransform.Value = result;
        }
    }

    public override void End(CursorButtonData data)
    {
        new CommandBuilder("Transform Image Layer", WorkingLayer)
            .SetProperty(_startTransform, e => e.Get<ImageLayerSetting>().ImageTransform)
            .Commit();
        Clear();
    }

    public override void Cancel()
    {
        _setting.ImageTransform.Value = _startTransform;
        Clear();
    }

    public void Clear()
    {
        _transformType = -1;
    }
}
