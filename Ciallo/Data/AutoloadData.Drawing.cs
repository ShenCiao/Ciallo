using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ciallo.Command;
using Ciallo.GuiControl;
using Ciallo.Tool;
using Frent;
using Godot;

namespace Ciallo.Data;

// The existing /root/AutoloadData node exposes these methods to Fennara/GDScript.
// JSON keeps the boundary independent of Frent and managed collection types.
public partial class AutoloadData
{
    private static readonly JsonSerializerOptions DrawingJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private Entity _drawingDocument;
    private string _drawingSession = "";
    private readonly Dictionary<string, Entity> _drawingObjects = [];
    private readonly Dictionary<Entity, string> _drawingIds = [];

    public string DrawingNewDocument(string name, Vector2 size) => DrawingBoundary(() =>
    {
        RequireDrawingIdle();
        RequireDrawing(!AppDocumentManager.WorkingDocumentModified, "Save the current document before replacing it.");
        RequireDrawing(!string.IsNullOrWhiteSpace(name) && size.IsFinite() && size.X > 0 && size.Y > 0,
            "A name and finite, positive canvas size are required.");
        var document = AppDocumentManager.Create(new DocumentSetting
        {
            Name = { Value = name }, ReferenceSize = { Value = size },
        });
        AppDocumentManager.WorkingDocument.Value = document;
        document.Get<CommandManager>().MarkUnsaved();
        return DrawingState();
    });

    public string DrawingOpenDocument(string path) => DrawingBoundary(() =>
    {
        RequireDrawingIdle();
        RequireDrawing(!AppDocumentManager.WorkingDocumentModified, "Save the current document before replacing it.");
        RequireDrawing(Path.IsPathFullyQualified(path) && File.Exists(path), "An existing absolute document path is required.");
        return OpenDocumentDialog.LoadWorldFile(path) ? DrawingState() : new { ok = false, error = "Document load failed." };
    });

    public string DrawingSaveDocument(string session, string version, string path) => DrawingBoundary(() =>
    {
        CheckDrawingVersion(session, version);
        RequireDrawing(Path.IsPathFullyQualified(path) && path.EndsWith(".ciallo", StringComparison.OrdinalIgnoreCase),
            "An absolute .ciallo path is required.");
        bool saved = string.Equals(_drawingDocument.Get<DocumentSetting>().FilePath.Value, path, StringComparison.Ordinal)
            ? AppDocumentManager.SaveWorkingDocument()
            : AppDocumentManager.SaveWorkingDocumentAs(path);
        return saved ? DrawingState() : new { ok = false, error = "Document save failed." };
    });

    public string DrawingGetState(string id, bool includeGeometry) =>
        DrawingBoundary(() => DrawingState(id, includeGeometry));

    public string DrawingHistory(string session, string version, bool redo) => DrawingBoundary(() =>
    {
        CheckDrawingVersion(session, version);
        var history = _drawingDocument.Get<CommandManager>();
        RequireDrawing(redo ? history.HasRedo : history.HasUndo, redo ? "Nothing to redo." : "Nothing to undo.");
        if (redo) history.Redo(); else history.Undo();
        return DrawingState();
    });

    public string DrawingApplyBatch(string json) => DrawingBoundary(() =>
    {
        var batch = JsonSerializer.Deserialize<DrawingBatch>(json, DrawingJson);
        RequireDrawing(batch != null && batch.Version != null && batch.Commands != null, "session, version and commands are required.");
        CheckDrawingVersion(batch.Session, batch.Version);
        var live = DrawingLiveObjects();
        var kinds = live.ToDictionary(pair => pair.Key, pair => DrawingKind(pair.Value));
        var counts = live.Where(pair => pair.Value.Has<LayerTreeNode>())
            .ToDictionary(pair => pair.Key, pair => pair.Value.Get<LayerTreeNode>().Children.Count);
        var parents = live.Where(pair => pair.Value.Has<LayerTreeNode>() && !pair.Value.IsDocument)
            .ToDictionary(pair => pair.Key, pair => DrawingId(pair.Value.Get<LayerTreeNode>().ParentValue));
        var touched = new HashSet<string>();

        // Validate the complete external batch before allocating entities or writing history.
        foreach (var edit in batch.Commands)
        {
            RequireDrawing(edit != null && !string.IsNullOrWhiteSpace(edit.Id) && touched.Add(edit.Id),
                "Each command needs an id; target each object at most once per batch.");
            bool creates = edit.Op is "layer" or "stroke_brush" or "fill_brush" or "stroke" or "polygon";
            if (creates)
            {
                RequireDrawing(!edit.Id.StartsWith('@') && edit.Id != "root" && !_drawingObjects.ContainsKey(edit.Id),
                    $"Id '{edit.Id}' is reserved or already used in this session.");
                kinds.Add(edit.Id, edit.Op);
            }
            else RequireDrawing(kinds.ContainsKey(edit.Id), $"Unknown object '{edit.Id}'.");
            string kind = kinds[edit.Id];
            switch (edit.Op)
            {
                case "layer":
                    RequireDrawing(edit.Parent == "root", "New shape layers are created at the document root.");
                    Insert(edit, "root");
                    counts.Add(edit.Id, 0);
                    break;
                case "stroke_brush": case "fill_brush": case "color":
                    RequireDrawing(kind is "stroke_brush" or "fill_brush", "color targets a brush.");
                    RequireDrawing(edit.Color != null && Color.HtmlIsValid(edit.Color), "color must be an HTML hex color.");
                    break;
                case "stroke": case "polygon":
                    Insert(edit, "layer");
                    CheckBrush(edit, kind);
                    PrepareGeometry(edit, kind);
                    break;
                case "geometry":
                    RequireDrawing(kind is "stroke" or "polygon", "geometry targets a stroke or polygon.");
                    PrepareGeometry(edit, kind);
                    break;
                case "brush":
                    RequireDrawing(kind is "stroke" or "polygon", "brush targets a stroke or polygon.");
                    CheckBrush(edit, kind);
                    break;
                case "layer_settings":
                    RequireDrawing(kind == "layer", "layer_settings targets a shape layer.");
                    RequireDrawing(edit.Opacity is null || float.IsFinite(edit.Opacity.Value) && edit.Opacity is >= 0 and <= 1,
                        "opacity must be between 0 and 1.");
                    break;
                case "move": case "delete":
                    RequireDrawing(kind is "stroke" or "polygon", "move/delete targets a stroke or polygon.");
                    counts[parents[edit.Id]]--;
                    if (edit.Op == "move") Insert(edit, "layer");
                    else kinds.Remove(edit.Id);
                    break;
                default: throw new DrawingInputException($"Unknown operation '{edit.Op}'.");
            }
        }

        var command = new CommandBuilder(batch.Name ?? "Drawing batch", _drawingDocument);
        Entity firstLayer = default;
        Entity firstStrokeBrush = default, firstFillBrush = default;
        foreach (var edit in batch.Commands)
        {
            if (edit.Op is "layer" or "stroke_brush" or "fill_brush" or "stroke" or "polygon")
            {
                var created = _drawingDocument.World.Create();
                _drawingObjects.Add(edit.Id, created);
                _drawingIds.Add(created, edit.Id);
            }
            var target = _drawingObjects[edit.Id];
            command.SetTarget(target);
            switch (edit.Op)
            {
                case "layer":
                    command.NewShapeLayer().SetProperty(e => e.Get<CommonLayerSetting>().Name, edit.Name ?? edit.Id)
                        .AddToLayerTree(_drawingDocument, (int)edit.Index);
                    if (firstLayer.IsNull) firstLayer = target;
                    break;
                case "stroke_brush":
                    if (firstStrokeBrush.IsNull) firstStrokeBrush = target;
                    command.NewStrokeBrush(new StrokeBrushSetting
                    {
                        Name = { Value = edit.Name ?? edit.Id }, Color = { Value = Color.FromHtml(edit.Color) },
                        RenderingType = { Value = BrushRenderingType.Vanilla },
                    });
                    break;
                case "fill_brush":
                    if (firstFillBrush.IsNull) firstFillBrush = target;
                    command.NewVectorFillBrush().SetProperty(e => e.Get<FillBrushSetting>().FillColor, Color.FromHtml(edit.Color));
                    break;
                case "stroke": case "polygon":
                    if (edit.Op == "stroke") command.NewStroke(); else command.NewFilledPolygon();
                    command.AddToLayerTree(_drawingObjects[edit.Parent], (int)edit.Index);
                    SetDrawingBrush(command, edit.Op, _drawingObjects[edit.Brush]);
                    SetDrawingGeometry(command, edit);
                    break;
                case "geometry": SetDrawingGeometry(command, edit); break;
                case "brush": SetDrawingBrush(command, DrawingKind(target), _drawingObjects[edit.Brush]); break;
                case "color":
                    if (target.Has<StrokeBrushSetting>())
                        command.SetProperty(e => e.Get<StrokeBrushSetting>().Color, Color.FromHtml(edit.Color));
                    else command.SetProperty(e => e.Get<FillBrushSetting>().FillColor, Color.FromHtml(edit.Color));
                    break;
                case "layer_settings":
                    if (edit.Name != null) command.SetProperty(e => e.Get<CommonLayerSetting>().Name, edit.Name);
                    if (edit.Visible.HasValue) command.SetProperty(e => e.Get<CommonLayerSetting>().IsVisible, edit.Visible.Value);
                    if (edit.Opacity.HasValue) command.SetProperty(e => e.Get<CommonLayerSetting>().Opacity, edit.Opacity.Value);
                    break;
                case "move": command.RemoveFromLayerTree().AddToLayerTree(_drawingObjects[edit.Parent], (int)edit.Index); break;
                case "delete": command.RemoveFromLayerTree().DeleteShape(); break;
            }
        }
        var selection = _drawingDocument.Get<SelectionManager>();
        if (!firstStrokeBrush.IsNull && selection.WorkingStrokeBrush.Value.IsNull)
            command.SetTarget(firstStrokeBrush).SetWorkingStrokeBrush();
        if (!firstFillBrush.IsNull && selection.WorkingVectorFillBrush.Value.IsNull)
            command.SetTarget(_drawingDocument).SetProperty(e => e.Get<SelectionManager>().WorkingVectorFillBrush, firstFillBrush);
        if (!firstLayer.IsNull && selection.SelectedLayers.Value.IsEmpty)
            command.SetTarget(firstLayer).SelectLayers();
        command.Commit();
        return DrawingState();

        void Insert(DrawingEdit edit, string parentKind)
        {
            RequireDrawing(edit.Parent != null && kinds.TryGetValue(edit.Parent, out var actual) && actual == parentKind,
                $"'{edit.Id}' requires a {parentKind} parent.");
            int count = counts[edit.Parent];
            RequireDrawing(edit.Index >= -1 && edit.Index <= count && edit.Index == Math.Truncate(edit.Index),
                "index must be -1 (append) or a valid integer insertion index.");
            if (edit.Index == -1) edit.Index = count;
            counts[edit.Parent]++;
            parents[edit.Id] = edit.Parent;
        }
        void CheckBrush(DrawingEdit edit, string kind)
        {
            string expected = kind == "stroke" ? "stroke_brush" : "fill_brush";
            RequireDrawing(edit.Brush != null && kinds.TryGetValue(edit.Brush, out var actual) && actual == expected,
                $"'{edit.Id}' requires a {expected}.");
        }
    });

    private object DrawingState(string id = "", bool includeGeometry = false)
    {
        if (AppDocumentManager.WorkingDocument.Value.IsNull) return new { ok = true, document = (object)null };
        EnsureDrawingDocument();
        var live = DrawingLiveObjects();
        RequireDrawing(id == "" || live.ContainsKey(id), $"Unknown object '{id}'.");
        var settings = _drawingDocument.Get<DocumentSetting>();
        var history = _drawingDocument.Get<CommandManager>();
        var selection = _drawingDocument.Get<SelectionManager>();
        return new
        {
            ok = true, session = _drawingSession, version = DrawingVersion(),
            name = settings.Name.Value, path = settings.FilePath.Value,
            size = new[] { settings.ReferenceSize.Value.X, settings.ReferenceSize.Value.Y },
            modified = history.DocumentModified.CurrentValue, canUndo = history.HasUndo, canRedo = history.HasRedo,
            selection = new
            {
                layers = selection.SelectedLayers.Value.Select(DrawingId).ToArray(),
                strokeBrush = DrawingId(selection.WorkingStrokeBrush.Value),
                fillBrush = DrawingId(selection.WorkingVectorFillBrush.Value),
            },
            objects = live.Where(pair => id == "" || pair.Key == id).Select(pair => Describe(pair.Key, pair.Value)).ToArray(),
        };

        object Describe(string key, Entity entity)
        {
            var row = new Dictionary<string, object> { ["id"] = key, ["kind"] = DrawingKind(entity) };
            if (entity.Has<LayerTreeNode>())
            {
                var node = entity.Get<LayerTreeNode>();
                row["parent"] = node.IsRoot ? "" : DrawingId(node.ParentValue);
                row["index"] = node.IsRoot ? 0 : node.Index;
                row["children"] = node.Children.Select(DrawingId).ToArray();
            }
            if (entity.Has<CommonLayerSetting>())
            {
                var layer = entity.Get<CommonLayerSetting>();
                row["name"] = layer.Name.Value; row["visible"] = layer.IsVisible.Value; row["opacity"] = layer.Opacity.Value;
            }
            if (entity.Has<StrokeBrushSetting>())
            {
                row["name"] = entity.Get<StrokeBrushSetting>().Name.Value;
                row["color"] = "#" + entity.Get<StrokeBrushSetting>().Color.Value.ToHtml();
            }
            if (entity.Has<FillBrushSetting>()) row["color"] = "#" + entity.Get<FillBrushSetting>().FillColor.Value.ToHtml();
            if (entity.Has<StrokeSetting>()) row["brush"] = DrawingId(entity.Get<StrokeSetting>().Brush.Value);
            if (entity.Has<FilledPolygonSetting>()) row["brush"] = DrawingId(entity.Get<FilledPolygonSetting>().BrushE.Value);
            if (entity.Has<SampledPolyline>())
            {
                var line = entity.Get<SampledPolyline>();
                row["pointCount"] = line.Count;
                if (includeGeometry)
                {
                    row["points"] = line.Positions.Value.Select(p => new[] { p.X, p.Y }).ToArray();
                    row["radii"] = line.Radii.Value.ToArray();
                }
            }
            return row;
        }
    }

    private Dictionary<string, Entity> DrawingLiveObjects()
    {
        var result = new Dictionary<string, Entity>();
        Visit(_drawingDocument);
        var brushes = _drawingDocument.Get<BrushManager>();
        foreach (var brush in brushes.StrokeBrushes.Concat(brushes.VectorFillBrushes)) result[DrawingId(brush)] = brush;
        return result;
        void Visit(Entity entity)
        {
            result[DrawingId(entity)] = entity;
            foreach (var child in entity.Get<LayerTreeNode>().Children) Visit(child);
        }
    }

    private void EnsureDrawingDocument()
    {
        var current = AppDocumentManager.WorkingDocument.Value;
        RequireDrawing(!current.IsNull, "Open or create a document first.");
        if (current == _drawingDocument) return;
        _drawingDocument = current;
        _drawingSession = Guid.NewGuid().ToString("N");
        _drawingObjects.Clear(); _drawingIds.Clear();
        _drawingObjects.Add("root", current); _drawingIds.Add(current, "root");
    }

    // Opaque strings survive Godot JSON's double-based number parsing without coercion.
    private string DrawingVersion() => _drawingDocument.Get<CommandManager>().PersistenceEpoch.ToString(CultureInfo.InvariantCulture);

    private void CheckDrawingVersion(string session, string version)
    {
        RequireDrawingIdle();
        EnsureDrawingDocument();
        RequireDrawing(session == _drawingSession && version == DrawingVersion(),
            "Stale session or version; call DrawingGetState before editing.");
    }

    private string DrawingId(Entity entity)
    {
        if (entity.IsNull) return "";
        if (_drawingIds.TryGetValue(entity, out var id)) return id;
        id = "@" + _drawingIds.Count;
        _drawingIds.Add(entity, id); _drawingObjects.Add(id, entity);
        return id;
    }

    private static string DrawingKind(Entity entity) => entity.IsDocument ? "root"
        : entity.Has<StrokeSetting>() ? "stroke" : entity.Has<FilledPolygonSetting>() ? "polygon"
        : entity.Has<StrokeBrushSetting>() ? "stroke_brush" : entity.Has<FillBrushSetting>() ? "fill_brush"
        : entity.Has<ShapeLayerSetting>() ? "layer" : "other";

    private static void SetDrawingBrush(CommandBuilder command, string kind, Entity brush)
    {
        if (kind == "stroke") command.SetProperty(e => e.Get<StrokeSetting>().Brush, brush);
        else command.SetProperty(e => e.Get<FilledPolygonSetting>().BrushE, brush);
    }

    private static void PrepareGeometry(DrawingEdit edit, string kind)
    {
        RequireDrawing(edit.Points != null && edit.Points.Length >= (kind == "stroke" ? 2 : 3), "Not enough points.");
        RequireDrawing(edit.Points.All(p => p != null && p.Length == 2 && p.All(float.IsFinite)), "points must contain finite [x,y] pairs.");
        var points = edit.Points.Select(p => new Vector2(p[0], p[1])).ToList();
        RequireDrawing(points.Zip(points.Skip(1)).All(pair => pair.First != pair.Second), "Consecutive points must differ.");
        RequireDrawing(points.Distinct().Count() >= (kind == "stroke" ? 2 : 3), "Not enough distinct points.");
        if (kind == "polygon" && points[0] != points[^1]) points.Add(points[0]);
        edit.Positions = [.. points];
        if (kind == "polygon") edit.PreparedRadii = [.. Enumerable.Repeat(1f, points.Count)];
        else
        {
            RequireDrawing(float.IsFinite(edit.Radius) && edit.Radius > 0, "radius must be finite and positive.");
            RequireDrawing(edit.Radii == null || edit.Radii.Length == points.Count && edit.Radii.All(r => float.IsFinite(r) && r > 0),
                "radii must have one finite, positive radius per point.");
            edit.PreparedRadii = edit.Radii == null ? [.. Enumerable.Repeat(edit.Radius, points.Count)] : [.. edit.Radii];
        }
    }

    private static void SetDrawingGeometry(CommandBuilder command, DrawingEdit edit) => command.SetSampledPolyline(
        edit.Positions, edit.PreparedRadii, [.. Enumerable.Repeat(1f, edit.Positions.Length)],
        [.. Enumerable.Repeat(Vector2.Zero, edit.Positions.Length)]);

    private static string DrawingBoundary(Func<object> operation)
    {
        try { return JsonSerializer.Serialize(operation(), DrawingJson); }
        catch (Exception error) when (error is JsonException or DrawingInputException)
        { return JsonSerializer.Serialize(new { ok = false, error = error.Message }, DrawingJson); }
    }

    private static void RequireDrawing(bool condition, string message)
    {
        if (!condition) throw new DrawingInputException(message);
    }

    private static void RequireDrawingIdle() => RequireDrawing(
        InteractionManager.StateMachine.State is not CapturingInteraction and not TimelineRolling,
        "Finish the active gesture and stop playback before editing through the drawing adapter.");

    private sealed class DrawingInputException(string message) : Exception(message);
    private sealed class DrawingBatch
    {
        public string Session { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public DrawingEdit[] Commands { get; set; }
    }
    private sealed class DrawingEdit
    {
        public string Op { get; set; }
        public string Id { get; set; }
        public string Parent { get; set; } = "root";
        public double Index { get; set; } = -1;
        public string Name { get; set; }
        public string Brush { get; set; }
        public string Color { get; set; }
        public float[][] Points { get; set; }
        public float Radius { get; set; } = 1f;
        public float[] Radii { get; set; }
        public bool? Visible { get; set; }
        public float? Opacity { get; set; }
        [JsonIgnore] public ImmutableArray<Vector2> Positions;
        [JsonIgnore] public ImmutableArray<float> PreparedRadii;
    }
}
