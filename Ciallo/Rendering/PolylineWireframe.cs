using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Ciallo.Rendering;

public partial class PolylineWireframe : Node2D
{
    public StrokeView Wireframe;
    public MultiMeshInstance2D Dots;

    public PolylineWireframe()
    {
        Wireframe = new() { Material = AutoloadRendering.WireframeMaterial };
        Dots = AutoloadRendering.CreateDots();

        AddChild(Wireframe);
        AddChild(Dots);
    }

    public void SetGeometry(IReadOnlyList<Vector2> points)
    {
        float wireframeRadius = AppPreference.StrokeWireframeRadius;
        float dotRadius = AppPreference.StrokeDotRadius;
        Wireframe.SetGeometry(points, wireframeRadius);
        Dots.SetDotGeometry(points, dotRadius);
    }
}

public static class DotExtension
{
    public static void SetDotGeometry(this MultiMeshInstance2D instance, IReadOnlyList<Vector2> points, IReadOnlyList<float> radii)
    {
        instance.Multimesh.InstanceCount = 0;
        if (points.Count == 0) return;

        if (points.Count != radii.Count) throw new ArgumentException("Positions and radii count mismatch.");

        var multiMesh = instance.Multimesh;
        multiMesh.UseCustomData = true;
        multiMesh.InstanceCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            var transform = Transform2D.Identity.Translated(points[i]);
            multiMesh.SetInstanceTransform2D(i, transform);
            multiMesh.SetInstanceColor(i, AppPreference.StrokeWireframeColor);
            multiMesh.SetInstanceCustomData(i, new(radii[i], 0, 0, 0));
        }
    }

    public static void SetDotGeometry(this MultiMeshInstance2D instance, IReadOnlyList<Vector2> points, float radius)
    {
        SetDotGeometry(instance, points, Enumerable.Repeat(radius, points.Count).ToArray());
    }
}
