using System.Collections.Generic;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

[SceneTree]
public partial class TimelineAction : Container
{
    public Entity Document;
    private SelectionManager _selectionManager;
    private TimelineSetting _timelineSetting;
    private Texture2D _playIcon;
    private Texture2D _stopIcon;
    private readonly ReactiveProperty<bool> _isPlaying = new(false);
    private double _playbackAccumulator;
    public ReadOnlyReactiveProperty<bool> IsPlaying => _isPlaying;

    public override void _Ready()
    {
        SetProcess(false);
        _playIcon = PlayStop.Icon;
        _stopIcon = GD.Load<Texture2D>("res://Icon/stop.svg");

        AddCelFolder.Pressed += OnAddCelFolder;
        NewAnimationCel.Pressed += OnNewAnimationCel;
        GoToStart.Pressed += () => NavigateToFrame(GetPlaybackStart());
        PreviousFrame.Pressed += () => NavigateRelative(-1);
        PlayStop.Pressed += TogglePlayback;
        NextFrame.Pressed += () => NavigateRelative(1);
        GoToEnd.Pressed += () => NavigateToFrame(GetPlaybackLastFrame());
    }

    public void Init(Entity document)
    {
        Document = document;
        _selectionManager = document.Get<SelectionManager>();
        _timelineSetting = document.Get<TimelineSetting>();
        var subs = new CompositeDisposable();
        NewAnimationCel.VisibleIf(_selectionManager.WorkingCelFolder, e => !e.IsNull, subs);
        LoopPlay.BindBool(_timelineSetting.LoopPlaybackEnabled, subs);
        OnionSkin.BindBool(_timelineSetting.OnionSkinEnabled, subs);
        FrameRate.BindNumber(_timelineSetting.FrameRate);
        subs.AddTo(document);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isPlaying.Value) return;
        if (@event is InputEventMouseMotion) return;
        if (@event is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Middle
                or MouseButton.WheelUp
                or MouseButton.WheelDown
                or MouseButton.WheelLeft
                or MouseButton.WheelRight
            }) return;
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton &&
            PlayStop.GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
            return;

        GetViewport().SetInputAsHandled();
    }

    private void NavigateRelative(int frameOffset)
    {
        if (_selectionManager == null) return;
        NavigateToFrame(_selectionManager.CurrentFrame.Value + frameOffset);
    }

    public override void _Process(double delta)
    {
        if (!_isPlaying.Value || _selectionManager == null || _timelineSetting == null) return;

        _playbackAccumulator += delta * Mathf.Max(_timelineSetting.FrameRate.Value, 1f);
        while (_playbackAccumulator >= 1.0 && _isPlaying.Value)
        {
            _playbackAccumulator -= 1.0;
            AdvancePlaybackFrame();
        }
    }

    private void NavigateToFrame(int targetFrame)
    {
        if (_selectionManager == null) return;

        int oldFrame = _selectionManager.CurrentFrame.Value;
        int newFrame = ClampPlaybackFrame(targetFrame);
        if (oldFrame == newFrame) return;

        var cmd = new CommandBuilder("Navigate Timeline")
            .SetProperty(_selectionManager.CurrentFrame, oldFrame, newFrame);

        var newWorkingLayer = _selectionManager.ResolveWorkingLayerForTimelineFrameSelection(newFrame);
        if (!newWorkingLayer.IsNull && (newWorkingLayer != _selectionManager.WorkingLayer.CurrentValue || _selectionManager.WorkingLayers.Value.Length > 1))
            cmd.SetTarget(newWorkingLayer).SetWorkingLayer();

        cmd.CommitOpenSequence();
    }

    private void TogglePlayback()
    {
        if (_isPlaying.Value)
        {
            SetPlaying(false);
            return;
        }

        StartPlayback();
    }

    private void StartPlayback()
    {
        if (_selectionManager == null || _timelineSetting == null) return;

        int frame = ClampPlaybackFrame(_selectionManager.CurrentFrame.Value);
        if (frame >= GetPlaybackLastFrame())
            frame = GetPlaybackStart();

        SetPlaying(true);
        SetFrameDirect(frame);
    }

    private void AdvancePlaybackFrame()
    {
        int currentFrame = ClampPlaybackFrame(_selectionManager.CurrentFrame.Value);
        int lastFrame = GetPlaybackLastFrame();

        if (currentFrame >= lastFrame)
        {
            if (_timelineSetting.LoopPlaybackEnabled.Value)
            {
                SetFrameDirect(GetPlaybackStart());
                return;
            }

            SetFrameDirect(lastFrame);
            SetPlaying(false);
            return;
        }

        int nextFrame = currentFrame + 1;
        SetFrameDirect(nextFrame);
        if (!_timelineSetting.LoopPlaybackEnabled.Value && nextFrame >= lastFrame)
            SetPlaying(false);
    }

    private void SetFrameDirect(int newFrame)
    {
        _selectionManager.CurrentFrame.Value = newFrame;
    }

    private int ClampPlaybackFrame(int frame) => Mathf.Clamp(frame, GetPlaybackStart(), GetPlaybackLastFrame());

    private int GetPlaybackStart() => _timelineSetting?.PlaybackStart.Value ?? 0;

    private int GetPlaybackLastFrame()
    {
        if (_timelineSetting == null) return 0;
        int start = _timelineSetting.PlaybackStart.Value;
        return Mathf.Max(start, _timelineSetting.PlaybackEnd.Value - 1);
    }

    private void SetPlaying(bool playing)
    {
        bool wasPlaying = _isPlaying.Value;
        if (wasPlaying && !playing)
            SwitchWorkingLayerAfterPlayback();

        _isPlaying.Value = playing;
        _playbackAccumulator = 0.0;
        PlayStop.Icon = playing ? _stopIcon : _playIcon;
        PlayStop.TooltipText = playing ? "Stop playback".Tr() : "Play".Tr();
        SetProcess(playing);
    }

    private void SwitchWorkingLayerAfterPlayback()
    {
        if (_selectionManager == null) return;

        int currentFrame = _selectionManager.CurrentFrame.Value;
        var newWorkingLayer = _selectionManager.ResolveWorkingLayerForTimelineFrameSelection(currentFrame);
        if (!newWorkingLayer.IsNull && (newWorkingLayer != _selectionManager.WorkingLayer.CurrentValue || _selectionManager.WorkingLayers.Value.Length > 1))
            new CommandBuilder("Playback Select Working Layer", newWorkingLayer).SetWorkingLayer().Do();
    }

    private void OnAddCelFolder()
    {
        var folder = Document.World.Create();
        var workingLayer = Document.Get<SelectionManager>().WorkingLayer.CurrentValue;
        var cursor = workingLayer.IsNull ? Document : workingLayer;
        Entity firstNonAnimFolder = Entity.Null;
        Entity animFolderParent = Entity.Null;

        while (true)
        {
            if (cursor.Has<FolderLayerSetting>())
            {
                if (cursor.Get<FolderLayerSetting>().IsCelFolder)
                {
                    animFolderParent = cursor.Get<LayerTreeNode>().ParentValue;
                    break;
                }
                if (firstNonAnimFolder.IsNull)
                    firstNonAnimFolder = cursor;
            }
            if (cursor.IsDocument) break;
            cursor = cursor.Get<LayerTreeNode>().ParentValue;
        }

        var parent = animFolderParent.IsNull ? firstNonAnimFolder : animFolderParent;

        new CommandBuilder("New Cel Folder", folder)
            .NewCelFolder()
            .AddToLayerTree(parent)
            .SetWorkingLayer()
            .Commit();
    }

    private void OnNewAnimationCel()
    {
        var celFolder = Document.Get<SelectionManager>().WorkingCelFolder.CurrentValue;
        if (celFolder.IsNull) return;

        int currentFrame = Document.Get<SelectionManager>().CurrentFrame.Value;
        (int frame, string name) = GetNewAnimationCelFrameName(celFolder, currentFrame);
        NewCelFromArchetype(celFolder, frame, name);
    }

    /// <summary>
    /// Builds a new cel under <paramref name="celFolder"/> at <paramref name="frame"/>, replicating the
    /// cel-child archetype schema, then exposes it and moves the playhead there — one undoable action.
    ///
    /// The cel is a regular folder named <paramref name="name"/>. Its contents come from
    /// <see cref="FolderLayerSetting.CelChildrenByName"/>: one child per surviving archetype name, cloned
    /// from that name's representative (the group's first member, matching the timeline archetype row).
    /// Empty archetype dict (the folder's first cel) falls back to a single blank shape layer.
    ///
    /// Self-contained on purpose: both entry points (toolbar button, cel right-click menu) differ only
    /// in how they pick (frame, name); everything after is identical, so it owns the CommandBuilder and
    /// the commit rather than taking a half-built one.
    /// </summary>
    internal static void NewCelFromArchetype(Entity celFolder, int frame, string name)
    {
        var document = celFolder.Document;
        var folderSetting = celFolder.Get<FolderLayerSetting>();
        var exposures = folderSetting.Exposures;
        bool replaceBlankExposure = exposures.ContainsKey(frame) && exposures[frame].IsCelFolder;
        var celE = celFolder.World.Create();

        var cmd = new CommandBuilder("New Animation Cel", celE)
            .NewFolderLayer()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, name)
            .AddToLayerTree(celFolder);

        // name -> the new cel's child carrying that name. Drives both VF reference remap and the
        // working-layer pick. Each surviving archetype name yields exactly one child, so this is 1:1.
        var newChildByName = new Dictionary<string, Entity>();
        var newVectorFillReps = new List<(Entity newChild, Entity representative)>();

        var archetypes = SelectArchetypeRepresentatives(celFolder);
        if (archetypes.Count == 0)
        {
            // Bootstrap (no archetypes yet) OR every archetype filtered out. Q2: an empty dict means the
            // folder's first cel, where there is no schema to follow, so seed a blank shape layer to draw
            // on. Q8: a non-empty dict that filters to nothing yields a deliberately empty cel folder.
            if (folderSetting.CelChildrenByName.Count == 0)
            {
                var shapeLayerE = celFolder.World.Create();
                cmd.SetTarget(shapeLayerE)
                    .NewShapeLayer()
                    .AddToLayerTree(celE)
                    .SetWorkingLayer();
            }
        }
        else
        {
            // Pass 1: clone each representative into the new cel, preserving layer order (archetypes are
            // pre-sorted by representative index). VF reference remap is deferred to pass 2 because a
            // fill layer may reference a sibling created later in this same pass.
            foreach (var (archetypeName, representative) in archetypes)
            {
                var newChildE = celFolder.World.Create();
                cmd.SetTarget(newChildE);
                CloneLayerByType(cmd, representative);
                cmd.AddToLayerTree(celE);

                newChildByName[archetypeName] = newChildE;
                if (representative.Has<VectorFillLayerSetting>())
                    newVectorFillReps.Add((newChildE, representative));
            }

            // Pass 2: rebuild each new fill layer's references by NAME. The representative's references
            // are cel-local shape siblings; we translate each to the same-named child in THIS cel.
            foreach (var (newChildE, representative) in newVectorFillReps)
            {
                var remapped = new List<Entity>();
                foreach (var oldRef in representative.Get<VectorFillLayerSetting>().ReferenceLayers)
                {
                    if (oldRef.IsNull || !oldRef.IsAlive || !oldRef.Has<CommonLayerSetting>())
                        continue;

                    string refName = oldRef.Get<CommonLayerSetting>().Name.Value;
                    if (newChildByName.TryGetValue(refName, out var newRef))
                        remapped.Add(newRef); // Q4: same-named child exists in the new cel — connect it.
                    else if (!HasCelAncestor(oldRef))
                        remapped.Add(oldRef); // Q4: a document-level shared layer (no cel ancestor) — keep as-is.
                    // else: unmapped AND lives inside some cel — drop it rather than point across cels.
                }

                if (remapped.Count > 0)
                {
                    var refs = remapped; // capture for the closure
                    cmd.SetTarget(newChildE).SetObservableCollection(
                        e => e.Get<VectorFillLayerSetting>().ReferenceLayers,
                        layers => layers.AddRange(refs));
                }
            }

            // Working layer follows the same rule as a cel-button click: the new cel's child sharing the
            // folder's preferred name. No match (empty preference, or that name was filtered) -> leave the
            // working layer untouched, same "rather not select than select wrong" stance as cel navigation.
            string preferredName = folderSetting.PreferredNameForCelSelection.Value;
            if (newChildByName.TryGetValue(preferredName, out var workingLayerE))
                cmd.SetTarget(workingLayerE).SetWorkingLayer();
        }

        cmd.SetTarget(celFolder)
            .SetObservableCollection(exposures, exp =>
            {
                if (replaceBlankExposure) exp.Remove(frame);
                exp.Add(frame, celE);
            })
            .SetTarget(document)
            .SetProperty(e => e.Get<SelectionManager>().CurrentFrame, frame)
            .Commit();
    }

    /// <summary>
    /// Selects the archetype names to replicate into a new cel and their representative layers, ordered to
    /// mirror layer order (ascending representative index, matching the timeline archetype rows).
    ///
    /// Filter (Q1.1-a): once the folder holds more than one cel, a name owned by a single layer is treated
    /// as a one-off, not part of the shared schema, and is skipped. While the folder still has at most one
    /// cel every name is necessarily single-membered, so the filter is disabled there to avoid producing an
    /// empty cel during bootstrap.
    /// </summary>
    private static List<(string name, Entity representative)> SelectArchetypeRepresentatives(Entity celFolder)
    {
        var celChildrenByName = celFolder.Get<FolderLayerSetting>().CelChildrenByName;
        bool filterSingletons = CountDirectCelChildren(celFolder) > 1;

        var result = new List<(string name, Entity representative)>();
        foreach (var (archetypeName, members) in celChildrenByName)
        {
            if (members.Count == 0) continue;
            if (filterSingletons && members.Count == 1) continue;

            Entity representative = default;
            foreach (var m in members) { representative = m; break; }
            if (representative.IsNull || !representative.IsAlive || !representative.Has<LayerTreeNode>())
                continue;

            result.Add((archetypeName, representative));
        }

        result.Sort((a, b) =>
            a.representative.Get<LayerTreeNode>().Index.CompareTo(b.representative.Get<LayerTreeNode>().Index));
        return result;
    }

    /// <summary>Counts a cel folder's direct cel children.</summary>
    private static int CountDirectCelChildren(Entity celFolder)
    {
        int count = 0;
        foreach (var child in celFolder.Get<LayerTreeNode>().Children)
            if (child.IsAlive && child.Tagged<CelTag>())
                count++;
        return count;
    }

    /// <summary>Dispatches the copy-based new-layer command for a representative's concrete layer type.
    /// Folder reps are cloned non-recursively (their own children are not archetype members).</summary>
    private static void CloneLayerByType(CommandBuilder cmd, Entity representative)
    {
        if (representative.Has<VectorFillLayerSetting>())
            cmd.NewVectorFillLayer(representative);
        else if (representative.Has<ShapeLayerSetting>())
            cmd.NewShapeLayer(representative);
        else if (representative.Has<ImageLayerSetting>())
            cmd.NewImageLayer(representative);
        else if (representative.Has<FolderLayerSetting>())
            cmd.NewFolderLayer(representative);
        else
            cmd.NewShapeLayer(); // Unknown type: fall back to a blank shape layer so the slot still exists.
    }

    /// <summary>True if <paramref name="layer"/> is a cel or has a cel ancestor.</summary>
    private static bool HasCelAncestor(Entity layer)
    {
        var cursor = layer;
        while (cursor.IsAlive && !cursor.IsDocument && cursor.Has<LayerTreeNode>())
        {
            if (cursor.Tagged<CelTag>())
                return true;

            cursor = cursor.Get<LayerTreeNode>().ParentValue;
        }

        return false;
    }

    /// <summary>
    /// Picks a frame and cel name for a new cel in <paramref name="celFolder"/>.
    /// </summary>
    public static (int, string) GetNewAnimationCelFrameName(Entity celFolder, int currentFrame)
    {
        var exposures = celFolder.Get<FolderLayerSetting>().Exposures;
        int frame = currentFrame;
        var usedNames = GetUsedCelNames(celFolder);

        if (exposures == null || exposures.Count == 0)
            return (frame, MakeUniqueNumericName(1, usedNames));

        if (exposures.ContainsKey(frame))
            frame = FindRhythmicUnoccupiedFrame(exposures, frame);

        return (frame, GetNewAnimationCelName(exposures, frame, usedNames));
    }

    internal static int FindRhythmicUnoccupiedFrame(ObservableSortedList<int, Entity> exposures, int currentFrame)
    {
        int currentIndex = exposures.FloorIndex(currentFrame);
        int candidate;

        if (currentIndex > 0)
        {
            int previousFrame = exposures.GetKeyAtIndex(currentIndex - 1);
            candidate = currentFrame + currentFrame - previousFrame;
        }
        else if (currentIndex + 1 < exposures.Count)
        {
            int nextFrame = exposures.GetKeyAtIndex(currentIndex + 1);
            candidate = currentFrame + nextFrame - currentFrame;
        }
        else
        {
            candidate = currentFrame + 1;
        }

        return FindNearestUnoccupiedFrame(exposures, candidate);
    }

    internal static int FindNearestUnoccupiedFrame(ObservableSortedList<int, Entity> exposures, int candidate)
    {
        int floorIndex = exposures.FloorIndex(candidate);
        if (floorIndex < 0 || exposures.GetKeyAtIndex(floorIndex) != candidate)
            return candidate;

        for (int distance = 1;; distance++)
        {
            long earlier = (long)candidate - distance;
            if (earlier >= int.MinValue && !exposures.ContainsKey((int)earlier))
                return (int)earlier;

            long later = (long)candidate + distance;
            if (later <= int.MaxValue && !exposures.ContainsKey((int)later))
                return (int)later;
        }
    }

    internal static string GetNewAnimationCelName(
        ObservableSortedList<int, Entity> exposures,
        int frame,
        HashSet<string> usedNames)
    {
        bool hasPrev = TryGetCelLabelNumber(exposures, exposures.FloorIndex(frame), out int prevNumber);
        bool hasNext = TryGetCelLabelNumber(exposures, exposures.CeilingIndex(frame), out int nextNumber);

        if (!hasPrev && !hasNext)
            return MakeUniqueNumericName(1, usedNames);

        if (hasPrev && !hasNext)
            return MakeUniqueNumericName(prevNumber + 1, usedNames);

        if (!hasPrev)
            return MakeUniqueNumericName(nextNumber - 1, usedNames);

        int gap = nextNumber - prevNumber;
        if (gap == 1)
            return MakeUniqueSuffixedName(prevNumber, usedNames);

        return MakeUniqueNumericName(prevNumber + 1, usedNames);
    }

    internal static bool TryGetCelLabelNumber(
        ObservableSortedList<int, Entity> exposures,
        int index,
        out int number)
    {
        number = 0;
        if (index < 0) return false;

        var cel = exposures.GetValueAtIndex(index);
        if (cel.IsNull || !cel.IsAlive || cel.IsCelFolder || !cel.Has<CommonLayerSetting>())
            return false;

        return TryParseCelLabel(cel.Get<CommonLayerSetting>().Name.Value, out number, out char suffix);
    }

    internal static bool TryParseCelLabel(string label, out int number, out char suffix)
    {
        number = 0;
        suffix = '\0';
        if (string.IsNullOrEmpty(label)) return false;

        int digitCount = 0;
        while (digitCount < label.Length && char.IsDigit(label[digitCount]))
            digitCount++;

        if (digitCount == 0) return false;

        if (digitCount == label.Length)
            return int.TryParse(label, out number);

        if (digitCount != label.Length - 1 || label[^1] is < 'a' or > 'z')
            return false;

        suffix = label[^1];
        return int.TryParse(label[..digitCount], out number);
    }

    internal static HashSet<string> GetUsedCelNames(Entity celFolder)
    {
        var usedNames = new HashSet<string>();
        foreach (var cel in celFolder.Get<LayerTreeNode>().Children)
        {
            if (cel.IsNull || !cel.IsAlive || !cel.Tagged<CelTag>() || !cel.Has<CommonLayerSetting>())
                continue;

            var name = cel.Get<CommonLayerSetting>().Name.Value;
            if (!string.IsNullOrEmpty(name))
                usedNames.Add(name);
        }

        return usedNames;
    }

    internal static string MakeUniqueNumericName(int baseNumber, HashSet<string> usedNames)
    {
        for (int number = baseNumber;; number++)
        {
            string candidate = number.ToString();
            if (!usedNames.Contains(candidate))
                return candidate;

            if (number == int.MaxValue)
                return MakeUniqueFallbackName(baseNumber, usedNames);
        }
    }

    internal static string MakeUniqueSuffixedName(int number, HashSet<string> usedNames)
    {
        for (char suffix = 'a'; suffix <= 'z'; suffix++)
        {
            string candidate = $"{number}{suffix}";
            if (!usedNames.Contains(candidate))
                return candidate;
        }

        return number == int.MaxValue
            ? MakeUniqueFallbackName(number, usedNames)
            : MakeUniqueNumericName(number + 1, usedNames);
    }

    internal static string MakeUniqueFallbackName(int number, HashSet<string> usedNames)
    {
        for (int i = 1;; i++)
        {
            string candidate = $"{number}_{i}";
            if (!usedNames.Contains(candidate))
                return candidate;
        }
    }
}
