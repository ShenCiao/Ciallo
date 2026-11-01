using System.Linq;
using Godot;

namespace Ciallo.Rendering;

// ReSharper disable once Godot.MissingParameterlessConstructor
public partial class TransformOverlayBox : Node2D
{
    public Vector2 Size;
    public Transform2D LocalTransform;
    public StrokeView Wireframe;
    public MultiMeshInstance2D Dots;

    public TransformOverlayBox(Vector2 size, Transform2D localTransform)
    {
        Size = size;
        LocalTransform = localTransform;
        Wireframe = new() { Material = AutoloadRendering.WireframeMaterial };

        Dots = AutoloadRendering.CreateDots();
        AddChild(Wireframe);
        AddChild(Dots);
        UpdateGeometry();
    }

    public TransformOverlayBox(Vector2 size) : this(size, Transform2D.Identity) // Transform2D.Identity is not static const
    {
    }

    public TransformOverlayBox(Vector2 size, Vector2 position) : this(size, new Transform2D(0, position))
    {
    }

    public void UpdateGeometry()
    {
        Vector2 half = Size * 0.5f;
        Vector2[] corners =
        [
            -half,
            new(-half.X, half.Y),
            half,
            new(half.X, -half.Y),
        ];
        corners = corners.Select(p => LocalTransform * p).ToArray();
        var barDir = (corners[0] - corners[1]).Normalized();
        var barLength = AppPreference.StrokeDotRadius * 4f;
        var topMid = (corners[0] + corners[3]) * 0.5f;
        Vector2 rotationDotPos = topMid + barLength * barDir;
        Wireframe.SetGeometry([rotationDotPos, topMid, ..corners, topMid], AppPreference.StrokeWireframeRadius);
        Dots.SetDotGeometry([..corners[..4], rotationDotPos], AppPreference.StrokeDotRadius * 2f);
    }

    public void UpdateGeometry(Rect2 rect)
    {
        Size = rect.Size;
        LocalTransform = new Transform2D(0, rect.GetCenter());
        UpdateGeometry();
    }
}
