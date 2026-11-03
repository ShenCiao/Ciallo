using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Ciallo.Geometry;
using Ciallo.Tool;
using Godot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObservableCollections;
using R3;

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
    [DataMember, JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
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
    public ToolPreferences Tools => InteractionManager.Tools;

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
            new PreferenceColorConverter(),
        }
    };

    public bool TryLoad()
    {
        if (AppCommandLineOptions.FactoryStartup) return false;
        string path = ProjectSettings.GlobalizePath(Path);
        if (!File.Exists(path))
            return false;
        try
        {
            PopulateFromJson(File.ReadAllText(path));
            return true;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Cannot load preferences '{path}': {e.Message}");
            if (e is JsonException or InvalidDataException)
                UserDataFiles.TryDelete(path);
            return false;
        }
    }

    internal void PopulateFromJson(string content)
    {
        // Tool preferences populate shared state instances, so snapshot them along with the root.
        string previous = JsonConvert.SerializeObject(this, JsonOptions);
        try
        {
            JsonConvert.PopulateObject(content, this, JsonOptions);
            if (Language.Value == null || RecentFiles == null || RecentFiles.Any(path => path == null) ||
                !float.IsFinite(UIScale.Value) || UIScale.Value < 0.1f || UIScale.Value > 2.0f ||
                PenPressureRemapCurve.Value.IsDefaultOrEmpty || PenPressureRemapCurve.Value.Length < 2 ||
                PenPressureRemapCurve.Value.Any(point => !point.P.IsFinite() || !point.In.IsFinite() || !point.Out.IsFinite()))
                throw new InvalidDataException("Preferences contain invalid UI or pen pressure settings.");
        }
        catch
        {
            JsonConvert.PopulateObject(previous, this, JsonOptions);
            throw;
        }
    }

    public void Save()
    {
        if (AppCommandLineOptions.FactoryStartup) return;
        var content = JsonConvert.SerializeObject(this, JsonOptions);
        UserDataFiles.WriteAtomic(ProjectSettings.GlobalizePath(Path), Encoding.UTF8.GetBytes(content));
    }

    // Godot's HSV/8-bit setters are derived views; replaying them would alter the saved RGBA channels.
    private sealed class PreferenceColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer) =>
            serializer.Serialize(writer, new { value.R, value.G, value.B, value.A });

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var data = JObject.Load(reader);
            float Channel(string name)
            {
                if (data[name]?.Type is not (JTokenType.Integer or JTokenType.Float))
                    throw new JsonSerializationException($"Invalid color channel '{name}'.");
                float value = data[name]!.Value<float>();
                if (!float.IsFinite(value)) throw new JsonSerializationException($"Invalid color channel '{name}'.");
                return value;
            }
            return new Color(Channel("R"), Channel("G"), Channel("B"), Channel("A"));
        }
    }

    #endregion

}
