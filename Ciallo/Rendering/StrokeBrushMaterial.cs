using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using Ciallo.Data;
using Ciallo.Geometry;
using Godot;
using R3;

namespace Ciallo.Rendering;

public partial class StrokeBrushMaterial : ShaderMaterial
{
    public CompositeDisposable Subs;

    public StrokeBrushMaterial()
    {
        Shader = AutoloadRendering.StrokeShader;
        ResourceLocalToScene = true;
    }

    public void ObserveBrushSetting(StrokeBrushSetting setting)
    {
        Subs?.Dispose();
        Subs = new();
        setting.RenderingType.Subscribe(type => SetShaderParameter("StrokeType", (int)type)).AddTo(Subs);
        setting.Color.Subscribe(color => SetShaderParameter("MaterialColor", color)).AddTo(Subs);
        setting.DashLength.Subscribe(length => SetShaderParameter("DashLength", length)).AddTo(Subs);
        setting.GapLength.Subscribe(length => SetShaderParameter("GapLength", length)).AddTo(Subs);
        setting.DashForwardSpeed.Subscribe(speed => SetShaderParameter("DashForwardSpeed", speed)).AddTo(Subs);
        // Brush-level flags
        setting.ActiveBrushFlags.Subscribe(value => SetShaderParameter("ActiveBrushFlags", (int)value)).AddTo(Subs);
        setting.BlendMode.Subscribe(value =>
        {
            Shader = value switch
            {
                // BlendMode.Add => AutoloadRendering.AddShader,
                // BlendMode.Multiply => AutoloadRendering.MultiplyShader,
                BlendMode.Erase => AutoloadRendering.EraserShader,
                _ => AutoloadRendering.StrokeShader,
            };
        }).AddTo(Subs);
        ImageTexture pp2FlowTex = null;
        setting.Pressure2FlowCurve.Subscribe(points =>
        {
            var img = BakeCurve(points);
            if (pp2FlowTex == null) pp2FlowTex = ImageTexture.CreateFromImage(img);
            else pp2FlowTex.Update(img);
            SetShaderParameter("Pressure2FlowCurve", pp2FlowTex);
        }).AddTo(Subs);

        // Stamp
        setting.ActiveStampFlags.Subscribe(value => SetShaderParameter("ActiveStampFlags", (int)value)).AddTo(Subs);
        setting.StampInterval.Subscribe(interval => SetShaderParameter("StampInterval", interval)).AddTo(Subs);
        setting.StampTexture.Subscribe(tex => SetShaderParameter("StampTexture", tex)).AddTo(Subs);
        setting.MaskTexture.Subscribe(tex => SetShaderParameter("MultiplyTexture", tex)).AddTo(Subs);
        ImageTexture diskOpacityTex = null;
        setting.DiskOpacityCurve.Subscribe(points =>
        {
            var img = BakeCurve(points);
            if (diskOpacityTex == null) diskOpacityTex = ImageTexture.CreateFromImage(img);
            else diskOpacityTex.Update(img);
            SetShaderParameter("DiskOpacityCurve", diskOpacityTex);
        }).AddTo(Subs);
        setting.StampRotation.Subscribe(rotation =>
        {
            var transform = new Transform2D(rotation, Vector2.Zero);
            SetShaderParameter("CoordinateTransform", transform);
        }).AddTo(Subs);

        setting.RotationNoiseAmplitude.Subscribe(value => SetShaderParameter("RotationNoiseAmplitude", value)).AddTo(Subs);

        // Airbrush
        ImageTexture falloffTex = null;
        setting.FalloffCurve.Subscribe(points =>
        {
            var img = BakeCurve(points);
            if (falloffTex == null) falloffTex = ImageTexture.CreateFromImage(img);
            else falloffTex.Update(img);
            SetShaderParameter("FalloffCurve", falloffTex);
        }).AddTo(Subs);
        setting.AlphaDensity.Subscribe(v => SetShaderParameter("AlphaDensity", v)).AddTo(Subs);
    }

    public override void _Notification(int what)
    {
        // TODO: Pitfall, Godot does not call NotificationPredelete on Resource class destruction.
        if (what == NotificationPredelete)
        {
            GD.Print("deleting brush material");
            Subs.Dispose();
        }
    }

    public static Image BakeCurve(ImmutableArray<BezierPoint> points)
    {
        int n = 512;
        var xs = Enumerable.Range(0, n).Select(i => (float)i / n).ToArray();
        var data = points.SampleXList(xs);
        var bytes = MemoryMarshal.AsBytes(data.AsSpan());
        var img = Image.CreateFromData(data.Length, 1, false, Image.Format.Rf, bytes);
        img.GenerateMipmaps();
        return img;
    }
}
