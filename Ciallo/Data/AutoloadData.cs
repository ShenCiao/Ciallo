using System;
using Godot;
using MessagePack;
using R3;

namespace Ciallo.Data;

public partial class AutoloadData : Node
{
    public static MessagePackSerializerOptions DefaultOption;

    public override void _EnterTree()
    {
        // // Godot delivers a high-report-rate device (e.g. 1000Hz mouse) as a burst of events at
        // // each ~60fps frame. Per-event TimeDelta (WorldEventDispatcher._timer) then measures
        // // processing time, not real input spacing: one event of the frame gets the full ~16ms,
        // // the other ~15 get ~0. Fed into InkStrokeModeler's spring integrator (position +=
        // // velocity*dt), it stalls through the near-zero steps then lurches on the big one ->
        // // per-frame wobble and stutter. A 150Hz pen has ~2 events/frame, so it never shows.
        // // Leaving accumulation ON (default) merges the burst into one event/frame at the correct
        // // dt; the modeler upsamples to its own output rate anyway, so feeding it the raw bursty
        // // timestamps is worse than not feeding all of them.
        // Input.UseAccumulatedInput = false;
        GetTree().AutoAcceptQuit = false;

        DefaultOption = MessagePackSerializer.DefaultOptions;
        // Compile persistence accessors and typed capture delegates before the first snapshot.
        PersistenceSnapshotCapture.WarmUp();
        AppDocumentDurability.Initialize(ProjectSettings.GlobalizePath("user://"));

        // Preference and load brush library data
        bool preferenceFileExists = AppPreference.TryLoad();
        if (!preferenceFileExists && !AppCommandLineOptions.FactoryStartup)
        {
            var idx = Preference.SupportedLanguages.IndexOf(OS.GetLocale(), LanguageComparer.Instance);
            if (idx != -1)
                AppPreference.Language.Value = Preference.SupportedLanguages[idx];
        }
        AppPreference.Language.Subscribe(TranslationServer.SetLocale).AddTo(this);
        AppPreference.UIScale.Debounce(TimeSpan.FromSeconds(0.75))
            .ObserveOn(GodotFrameProvider.Process)
            .Subscribe(scale => GetTree().Root.ContentScaleFactor = Mathf.Clamp(scale, 0.1f, 2.0f))
            .AddTo(this);

        if (preferenceFileExists)
        {
            GetWindow().Mode = AppPreference.WindowMode;
            if (AppPreference.WindowMode == Window.ModeEnum.Windowed)
            {
                GetWindow().SetPosition(AppPreference.WindowPosition);
                GetWindow().SetSize(AppPreference.WindowSize);
            }
        }

        GetWindow().SizeChanged += () =>
        {
            var window = GetWindow();
            AppPreference.WindowMode = window.GetMode();

            if (window.GetMode() == Window.ModeEnum.Windowed)
            {
                AppPreference.WindowPosition = GetWindow().Position;
                AppPreference.WindowSize = GetWindow().Size;
            }
        };

        bool brushFilesExists = AppStrokeBrushLibrary.TryLoad();
        if (!brushFilesExists) AppStrokeBrushLibrary.ResetBuiltInBrushes();

        AppMarkerTextureLibrary.Initialise();
    }

    public override void _Process(double delta)
    {
        AppDocumentDurability.Process(delta);
    }

    private bool _closing;

    // ReSharper disable once AsyncVoidMethod
    public override async void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            // async void is re-entrant: without this guard a second close request while the first
            // is still awaiting runs the whole teardown again (completing the recovery channel
            // twice, disposing the cloud twice).
            if (_closing) return;
            _closing = true;
            bool closeConfirmed = false;
            try
            {
                if (!await AppDocumentManager.UserCloseWorkingDocument()) return;
                closeConfirmed = true;

                try
                {
                    // Publish the session close marker before flushing and shutting down cloud sync.
                    await AppDocumentDurability.ShutdownAsync();
                    if (SteamManager.IsRecoveryCloudAvailable)
                        await SteamManager.FlushRecoverySessionAsync(TimeSpan.FromSeconds(5));
                }
                finally
                {
                    await SteamManager.ShutdownRecoveryCloudAsync();
                }
            }
            catch (Exception exception)
            {
                GD.PrintErr($"Cannot complete application shutdown: {exception}");
            }
            finally
            {
                if (closeConfirmed)
                {
                    try
                    {
                        SaveUserData(AppStrokeBrushLibrary.Save, "brush library");
                        SaveUserData(AppMarkerTextureLibrary.Save, "marker library");
                        SaveUserData(AppPreference.Save, "preferences");
                        AppDocumentManager.Clear();
                    }
                    finally
                    {
                        GetTree().Quit();
                    }
                }
                else
                {
                    _closing = false;
                }
            }
            return;
        }

        // Force garbage collection makes godot memory leak warning disappear
        if (what != NotificationPredelete) return;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        GC.WaitForPendingFinalizers();
    }

    private static void SaveUserData(Action save, string description)
    {
        try
        {
            save();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Cannot save {description} during shutdown: {exception}");
        }
    }
}
