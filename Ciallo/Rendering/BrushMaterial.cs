using System.Linq;
using System.Runtime.InteropServices;
using Ciallo.Data;
using Ciallo.Geometry;
using Godot;
using R3;

namespace Ciallo.Rendering;

public partial class BrushMaterial : ShaderMaterial
{
    public CompositeDisposable Subs;
    public static readonly Shader StrokeShader = GD.Load<Shader>("res://Rendering/Stroke.gdshader");

    private static BrushMaterial _missingBrushMaterial;
    public static BrushMaterial MissingBrushMaterial
    {
        get
        {
            if (_missingBrushMaterial != null) return _missingBrushMaterial;
            var m = _missingBrushMaterial = new();
            m.SetShaderParameter("strokeType", 0);
            m.SetShaderParameter("materialColor", Colors.Crimson);
            m.SetShaderParameter("dashLength", 5f);
            m.SetShaderParameter("dashForwardSpeed", 7f);
            return m;
        }
    }

    public BrushMaterial()
    {
        Shader = StrokeShader;
        ResourceLocalToScene = true;
    }
    
    public void ObserveBrushSetting(BrushSetting setting)
    {
        Subs?.Dispose();
        Subs = new();
        setting.RenderingType.Subscribe(type => SetShaderParameter("strokeType", (int)type)).AddTo(Subs);
        setting.Color.Subscribe(color => SetShaderParameter("materialColor", color)).AddTo(Subs);
        setting.DashLength.Subscribe(length => SetShaderParameter("dashLength", length)).AddTo(Subs);
        setting.GapLength.Subscribe(length => SetShaderParameter("gapLength", length)).AddTo(Subs);
        setting.DashForwardSpeed.Subscribe(speed => SetShaderParameter("dashForwardSpeed", speed)).AddTo(Subs);
        
        // Stamp
        setting.StampInterval.Subscribe(interval => SetShaderParameter("stampInterval", interval)).AddTo(Subs);
        SetShaderParameter("stampTexture", setting.StampTexture);
        SetShaderParameter("multiplyTexture", setting.MultiplyTexture);
        setting.StampRotation.Subscribe(rotation =>
        {
            var transform = new Transform2D(rotation, Vector2.Zero);
            SetShaderParameter("coordinateTransform", transform);
        }).AddTo(Subs);
        setting.RotationNoiseOctave.Subscribe(value => SetShaderParameter("rotationNoiseOctave", value)).AddTo(Subs);
        setting.RotationNoiseAmplitude.Subscribe(amp => SetShaderParameter("rotationNoiseAmplitude", amp)).AddTo(Subs);
        setting.RotationNoiseFrequency.Subscribe(freq => SetShaderParameter("rotationNoiseFrequency", freq)).AddTo(Subs);
        
        var falloffTex = ImageTexture.CreateFromImage(BakeCurve(setting.FalloffCurve));
        setting.FalloffCurve.Changed.Prepend(new Unit()).Subscribe(_ =>
        {
            falloffTex.Update(BakeCurve(setting.FalloffCurve));
            SetShaderParameter("falloffCurve", falloffTex);
        }).AddTo(Subs);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            GD.Print("deleting brush material");
            Subs.Dispose();
        }
    }

    public static Image BakeCurve(BezierCurve curve)
    {
        int n = 512;
        var data = curve.SampleXList(Enumerable.Range(0, n).Select(i => (float)i / n).ToArray());
        var bytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(data));
        var img = Image.CreateFromData(data.Count, 1, false, Image.Format.Rf, bytes);
        img.GenerateMipmaps();
        return img;
    }
}