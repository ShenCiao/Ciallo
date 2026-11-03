using Ciallo.Data;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

[SceneTree, Instantiable]
public partial class StrokeBrushEditor : AcceptDialog
{
    protected Entity Document;

    public void Init(Entity document)
    {
        Document = document;
        var sm = Document.Get<SelectionManager>();
        BrushPreviewList.Init(document);
        BindProperty(sm.WorkingStrokeBrush.Select(e => e.TryGet<StrokeBrushSetting>())).AddTo(document);
        BrushPreviewList.EditButton.Visible = false;
    }

    public CompositeDisposable BindProperty(Observable<StrokeBrushSetting> workingBrush)
    {
        CompositeDisposable subs = new();
        var setting = new StrokeBrushSetting
        {
            Name = workingBrush.Select(s => s?.Name).Flatten().AddTo(subs),
            Color = workingBrush.Select(s => s?.Color).Flatten().AddTo(subs),
            ActiveBrushFlags = workingBrush.Select(s => s?.ActiveBrushFlags).Flatten().AddTo(subs),
            BlendMode = workingBrush.Select(s => s?.BlendMode).Flatten().AddTo(subs),
            BaseRadius = workingBrush.Select(s => s?.BaseRadius).Flatten().AddTo(subs),
            Pressure2RadiusCurve = workingBrush.Select(s => s?.Pressure2RadiusCurve).Flatten().AddTo(subs),
            RenderingType = workingBrush.Select(s => s?.RenderingType).Flatten().AddTo(subs),
            Pressure2FlowCurve = workingBrush.Select(s => s?.Pressure2FlowCurve).Flatten().AddTo(subs),
            // Vanilla
            DashLength = workingBrush.Select(s => s?.DashLength).Flatten().AddTo(subs),
            GapLength = workingBrush.Select(s => s?.GapLength).Flatten().AddTo(subs),
            DashForwardSpeed = workingBrush.Select(s => s?.DashForwardSpeed).Flatten().AddTo(subs),
            // Stamp
            ActiveStampFlags = workingBrush.Select(s => s?.ActiveStampFlags).Flatten().AddTo(subs),
            StampInterval = workingBrush.Select(s => s?.StampInterval).Flatten().AddTo(subs),
            StampTexture = workingBrush.Select(s => s?.StampTexture).Flatten().AddTo(subs),
            DiskOpacityCurve = workingBrush.Select(s => s?.DiskOpacityCurve).Flatten().AddTo(subs),
            StampRotation = workingBrush.Select(s => s?.StampRotation).Flatten().AddTo(subs),
            MaskTexture = workingBrush.Select(s => s?.MaskTexture).Flatten().AddTo(subs),
            RotationNoiseAmplitude = workingBrush.Select(s => s?.RotationNoiseAmplitude).Flatten().AddTo(subs),
            // Airbrush
            FalloffCurve = workingBrush.Select(s => s?.FalloffCurve).Flatten().AddTo(subs),
            AlphaDensity = workingBrush.Select(s => s?.AlphaDensity).Flatten().AddTo(subs),
        };

        setting.DrawProperty(PropertiesHolder);

        PropertiesHolder.VisibleIf(workingBrush, s => s != null);
        return subs;
    }
}
