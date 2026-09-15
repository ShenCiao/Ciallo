using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using Ciallo.Geometry;
using Ciallo.Tool;
using Godot;
using Newtonsoft.Json;
using ObservableCollections;
using R3;
using FileAccess = Godot.FileAccess;

namespace Ciallo.Data;

[DataContract]
public class Preference
{
    public readonly ReactiveProperty<float> MouseWheelZoomFactor = new(0.1f);
    public readonly ReactiveProperty<float> MouseWheelRotateFactor = new(Mathf.Pi / 36);

    public static readonly List<string> SupportedLanguages =
    [
        "en",
        "fr",
        "de",
        "ja",
        "ko",
        "zh_CN",
        "zh_TW",
    ];

    [DataMember]
    public Window.ModeEnum WindowMode;
    [DataMember]
    public Vector2I WindowPosition = new(0, 0);
    [DataMember]
    public Vector2I WindowSize = new(1920, 1080);
    [DataMember]
    public ReactiveProperty<string> Language = new("en");
    [DataMember]
    public ReactiveProperty<float> UIScale = new(1.0f);
    [DataMember]
    public ObservableList<string> RecentFiles = [];
    [DataMember]
    public ReactiveProperty<ToolButton.Type?> PressedToolButton = new(ToolButton.Type.PaintStroke);
    [DataMember]
    public ReactiveProperty<int> CommandHistoryLimit = new(50);

    [DataMember]
    public ReactiveProperty<TimeSpan> RecoverySnapshotInterval = new(TimeSpan.FromMinutes(5));
    [DataMember]
    public ReactiveProperty<int> RecoverySnapshotLimitPerDocument = new(24);
    [DataMember]
    public ReactiveProperty<int> RecoverySnapshotAccountFileLimit = new(256);
    [DataMember]
    public ReactiveProperty<long> RecoverySnapshotAccountByteLimit = new(2L * 1024 * 1024 * 1024);

    [DataMember]
    public Color StrokeWireframeColor = Colors.Orange;
    [DataMember]
    public float StrokeWireframeRadius = 2f;
    [DataMember]
    public float StrokeDotRadius = 12f;

    [DataMember]
    public ReactiveProperty<ImmutableArray<BezierPoint>> PenPressureRemapCurve = new(BezierCurveFactory.Linear());

    [DataMember]
    public ReactiveProperty<bool> PaintStrokeSnapEnabled = new(false);
    [DataMember]
    public ReactiveProperty<float> PaintStrokeSnapDistance = new(24f);
    [DataMember]
    public ReactiveProperty<int> PaintStrokeMode = new(0); // 0 = Freehand, 1 = Bezier, 2 = PolyCubicBezier

    #region Save Load Json

    public static readonly string Path = "user://Preference.json";

    // Nested preference objects use [DataContract] and [DataMember], just like this root.
    // Their initialized fields supply defaults when loading older JSON that omits them.
    // PopulateObject reuses nested instances; ReactivePropertyConverter updates Value in place,
    // preserving existing bindings/subscriptions while storing only the inner value in JSON.
    public static readonly JsonSerializerSettings JsonOptions = new()
    {
        Converters =
        {
            ReactivePropertyConverter.Instance,
        }
    };

    public bool TryLoad()
    {
        if (AppCommandLineOptions.FactoryStartup) return false;
        if (!FileAccess.FileExists(Path))
            return false;
        try
        {
            using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
            string content = file.GetAsText();
            JsonConvert.PopulateObject(content, this, JsonOptions);
            return true;
        }
        catch (Exception e)
        {
            GD.PrintRaw($"Failed to load preference: {e}");
            return false;
        }
    }

    public void Save()
    {
        if (AppCommandLineOptions.FactoryStartup) return;
        var content = JsonConvert.SerializeObject(this, JsonOptions);
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file.StoreString(content);
    }

    #endregion

    #region Tool

    [DataMember]
    public BucketFillOptions BucketFill = new();

    [DataMember]
    public ReactiveProperty<float> VectorFillMarkerRadius = new(15.0f);

    [DataMember]
    public ReactiveProperty<Color?> VectorFillLayerBoundedAreaColor = new(new(0.62f, 0.62f, 0.62f, 1.0f));

    [DataMember]
    public ReactiveProperty<bool> ShowVectorFillReferenceLayerWireframe = new(false);
    [DataMember]
    public ReactiveProperty<float> GapBridgeDetectMaxGapLength = new(24f);
    [DataMember]
    public ReactiveProperty<float> GapBridgeHitRadius = new(6f);

    #endregion

}
