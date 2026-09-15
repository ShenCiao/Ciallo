using System;
using System.IO;
using System.Linq;
using Ciallo.Diagnostics;
using Ciallo.GuiControl;
using Godot;

namespace Ciallo.CommandLine;

// First autoload: configure persistence before Keychain and product autoloads enter the tree.
public partial class AutoloadCommandLine : Node
{
    public override void _EnterTree()
    {
        try
        {
            // The engine opens its log before autoloads. Retain that original path.
            _ = AppBugReport.LogFilePath;
            // Godot 4.6 leaves non-scene filenames before '--' available to applications.
            // Application switches belong after '--'; Fennara adds that separator itself.
            var bareDocuments = OS.GetCmdlineArgs()
                .Where(arg => !arg.StartsWith('-') && arg.EndsWith(".ciallo", StringComparison.OrdinalIgnoreCase));
            AppCommandLineOptions.Initialize(bareDocuments.Concat(OS.GetCmdlineUserArgs()).ToArray());
            if (AppCommandLineOptions.UserDataDirectory != null)
                UserDataDirectoryOverride.Apply(AppCommandLineOptions.UserDataDirectory);
            AppCommandLineOptions.ResolveDocumentPath();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            GD.PrintErr($"Ciallo startup: {exception.Message}\nUsage: Ciallo -- [FILE | --open FILE] [--user-data-dir DIR] [--factory-startup] [--debug-info]");
            // Quit is deferred by Godot. Stop here before other autoloads read/write a profile.
            System.Environment.Exit(2);
        }
    }

    public override void _Ready()
    {
        // All autoloads exist now, but Keychain's _Ready has not run yet.
        GetNode<Node>("/root/Keychain").Set("persistence_enabled", !AppCommandLineOptions.FactoryStartup);
        // The root becomes ready after every autoload and the main scene's children.
        GetTree().Root.Connect(Node.SignalName.Ready, Callable.From(ApplySceneOptions), (uint)ConnectFlags.OneShot);
    }

    private void ApplySceneOptions()
    {
        if (AppCommandLineOptions.DebugInfo)
            GD.Print(AppBugReport.CollectSystemInfo());
        if (AppCommandLineOptions.DocumentPath == null) return;

        if (OpenDocumentDialog.LoadWorldFile(AppCommandLineOptions.DocumentPath))
            AppBugReport.Note($"Startup document opened: {AppCommandLineOptions.DocumentPath}");
        else
            GD.PrintErr($"Ciallo startup: could not open '{AppCommandLineOptions.DocumentPath}'.");
    }
}
