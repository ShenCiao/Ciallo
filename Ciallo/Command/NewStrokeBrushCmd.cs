using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.Command;

[CommandBuilder]
public class NewStrokeBrushCmd : CommandBase
{
    private StrokeBrushSetting _setting;
    public readonly Entity CopyE;

    public NewStrokeBrushCmd(StrokeBrushSetting setting = null)
    {
        _setting = setting;
    }

    public NewStrokeBrushCmd(Entity copyE = default)
    {
        CopyE = copyE;
    }

    public override void OnDeletedAsDo() => TargetE.Delete();

    public override void BeforeFirstDo(Entity targetE)
    {
        _setting ??= CopyE.IsNull
            ? new StrokeBrushSetting()
            : CopyE.Get<StrokeBrushSetting>().Clone();
        targetE.Add(_setting);

        // material
        var material = new StrokeBrushMaterial();
        material.ObserveBrushSetting(_setting);
        targetE.Add(material);

        // preview texture
        Vector2I size = new(256, 96);
        var viewport = new SubViewport()
        {
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
            Size = size,
            UseHdr2D = true,
            Disable3D = true,
            TransparentBg = true,
        }.QueueFreeWith(targetE);

        var previewStroke = new StrokeView()
        {
            Material = material,
        };
        Document.Get<ColorManager>().CurrentColor.Subscribe(color =>
            previewStroke.SetInstanceShaderParameter("ShapeColor", color)).AddTo(targetE);
        float gv = 16;
        var previewRect = new Rect2(Vector2.Zero, size).GrowIndividual(-gv, -1.5f * gv, -gv, -1.5f * gv);
        _setting.IndependentRadius.CombineLatest(Document.Get<BrushManager>().CurrentStrokeRadius,
                (own, global) => own ?? global)
            .CombineLatest(_setting.Pressure2RadiusCurve, _setting.ActiveBrushFlags, ValueTuple.Create)
            .Subscribe(combo =>
            {
                var (r, pts, flags) = combo;
                int n = 32;
                float pi = Mathf.Pi;
                bool pressure2Radius = flags.HasFlag(BrushFlags.Pressure2Radius);
                var radius = Enumerable.Range(0, n)
                    .Select(i => Mathf.Lerp(-pi / 2, pi / 2, (float)i / (n - 1)))
                    .Select(Mathf.Cos) // pen pressure
                    .Select(x => pressure2Radius ? pts.SampleX(x) : 1.0f)
                    .Select(ratio => ratio * r.SigmoidRemap(1, 12, 1, 36))
                    .ToArray();
                previewStroke.SetGeometry(CreatePreviewGeometry(previewRect, n), radius);
            }).AddTo(targetE);

        // background
        var bg = new HBoxContainer()
        {
            Size = new Vector2(size.X, size.Y),
        };
        bg.AddThemeConstantOverride("separation", 0);
        bg.AddChild(new ColorRect()
        {
            Color = Colors.White,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        });
        bg.AddChild(new ColorRect()
        {
            Color = new(0.5f, 0.5f, 0.5f, 1.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        });
        viewport.AddChild(bg);
        viewport.AddChild(previewStroke);

        Document.Get<SubViewportHolder>().AddChild(viewport);
        targetE.Add(viewport.GetTexture());
    }

    public override void Do(Entity targetE)
    {
        // Data
        targetE.Tag<ToSerializeTag>();
        var bm = Document.Get<BrushManager>();
        bm.StrokeBrushes.Add(targetE);
    }

    public override void Undo(Entity brushE)
    {
        var bm = Document.Get<BrushManager>();
        bm.StrokeBrushes.Remove(brushE);
        brushE.Detach<ToSerializeTag>();
    }


    private static List<Vector2> CreatePreviewGeometry(Rect2 dst, int numPoints = 32)
    {
        var points = new List<Vector2>(numPoints);

        float pi = Mathf.Pi;
        // x ∈ [-π, π], y ∈ [-1, 1]
        Rect2 src = new Rect2(-pi, -1f, 2f * pi, 2f);
        Vector2 scale = dst.Size / src.Size;
        Vector2 srcCenter = src.GetCenter();
        Vector2 dstCenter = dst.GetCenter();
        Transform2D transform = new Transform2D(0f, scale, 0f, dstCenter - srcCenter * scale);

        for (int i = 0; i < numPoints; i++)
        {
            float x = Mathf.Lerp(-pi, pi, (float)i / (numPoints - 1));
            float y = Mathf.Sin(x);
            points.Add(transform * new Vector2(x, y));
        }
        return points;
    }
}
