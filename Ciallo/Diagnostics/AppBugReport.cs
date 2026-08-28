using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Tool;
using Frent;
using Godot;
using Environment = System.Environment;

namespace Ciallo.Diagnostics;

public static class AppBugReport
{
    private const int MaxBreadcrumbs = 120;
    private const int LogTailLines = 220;
    private const int LogTailChars = 32000;
    private const string GodotLogPath = "user://logs/ciallo.log";

    private static readonly object Gate = new();
    private static readonly Queue<string> Breadcrumbs = [];
    private static bool _exceptionHandlersInstalled;

    public static string LogFilePath => ProjectSettings.GlobalizePath(GodotLogPath);
    public static string LogDirectoryPath => Path.GetDirectoryName(LogFilePath) ?? OS.GetUserDataDir();

    public static void InstallExceptionHandlers()
    {
        if (_exceptionHandlersInstalled) return;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _exceptionHandlersInstalled = true;

        Note("Diagnostics started");
    }

    public static void UninstallExceptionHandlers()
    {
        if (!_exceptionHandlersInstalled) return;

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _exceptionHandlersInstalled = false;
    }

    public static void Note(string message) => Add(message, false);

    public static void Exception(Exception exception)
    {
        Add($"{exception.GetType().FullName}: {exception.Message}\n{exception}", true);
    }

    public static void Command(string source, string actionName, IReadOnlyList<ICommand> commands, bool execute)
    {
        Note($"{source}: {actionName}; execute={execute}; commands={SummarizeCommands(commands)}");
    }

    public static void Undo(string actionName) => Note($"Undo: {actionName}");

    public static void Redo(string actionName) => Note($"Redo: {actionName}");

    public static void ToolSwitch(ToolButton.Type? button, InteractionState state, Entity layerE)
    {
        string stateName = state?.GetType().Name ?? "<none>";
        string buttonName = button?.ToString() ?? "<none>";
        Note($"Tool switch: {buttonName}; {stateName}; layer={DescribeLayer(layerE)}");
    }

    public static void CopyMarkdownToClipboard()
    {
        DisplayServer.ClipboardSet(BuildMarkdown());
    }

    public static void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LogDirectoryPath);
            OS.ShellOpen(LogDirectoryPath);
        }
        catch (Exception exception)
        {
            Exception(exception);
        }
    }

    public static string BuildMarkdown()
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Report generated at: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
        builder.AppendLine();
        AppendCodeBlock(builder, CollectAppState());
        builder.AppendLine(
            """

            ## System Info

            """);
        AppendCodeBlock(builder, CollectSystemInfo());
        builder.AppendLine(
            """

            ## Recent Ciallo Events

            """);
        AppendCodeBlock(builder, CollectBreadcrumbs());
        builder.AppendLine(
            """

            ## Godot Log Tail

            """);
        AppendCodeBlock(builder, ReadLogTail());

        return builder.ToString().TrimEnd();
    }

    public static string CollectSystemInfo()
    {
        var driverInfo = OS.GetVideoAdapterDriverInfo();
        return
            $$"""
            Ciallo: {{ProjectSettings.GetSetting("application/config/version", "unknown")}}
            Godot: {{Engine.GetVersionInfo()["string"]}}
            .NET: {{RuntimeInformation.FrameworkDescription}}
            OS: {{Environment.OSVersion}}
            OS name: {{OS.GetName()}} {{OS.GetVersion()}}
            Locale: {{OS.GetLocale()}}
            CPU: {{CollectCpuName()}} ({{OS.GetProcessorCount()}} threads)
            Process architecture: {{RuntimeInformation.ProcessArchitecture}}
            GPU: {{RenderingServer.GetVideoAdapterName()}}
            GPU vendor: {{RenderingServer.GetVideoAdapterVendor()}}
            GPU API: {{RenderingServer.GetVideoAdapterApiVersion()}}
            Driver: {{driverInfo[0]}} {{driverInfo[1]}}
            Rendering driver: {{RenderingServer.GetCurrentRenderingDriverName()}}
            Display server: {{DisplayServer.GetName()}}
            Tablet driver: {{DisplayServer.TabletGetCurrentDriver()}}
            User data: {{OS.GetUserDataDir()}}
            Log file: {{LogFilePath}}
            """;
    }

    public static string DescribeLayer(Entity entity)
    {
        if (entity.IsNull) return "<none>";
        if (!entity.IsAlive) return $"<dead {entity.PackedValue}>";
        if (entity.IsDocument) return "<document>";

        string name = entity.Has<CommonLayerSetting>()
            ? entity.Get<CommonLayerSetting>().Name.Value
            : "<unnamed>";
        string kind = DescribeLayerKind(entity);
        return $"{kind} \"{name}\"";
    }

    private static void Add(string message, bool error)
    {
        string entry = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}";
        lock (Gate)
        {
            Breadcrumbs.Enqueue(entry);
            while (Breadcrumbs.Count > MaxBreadcrumbs)
                Breadcrumbs.Dequeue();
        }

        if (error)
            GD.PrintErr($"[Ciallo] {message}");
        else
            GD.Print($"[Ciallo] {message}");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            Exception(exception);
        else
            Add($"Unhandled non-Exception object: {e.ExceptionObject}", true);
    }

    private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Exception(e.Exception);
    }

    private static string CollectAppState()
    {
        var document = AppDocumentManager.WorkingDocument.Value;
        if (document.IsNull || !document.IsAlive)
            return "No working document.";

        var settings = document.Get<DocumentSetting>();
        var selection = document.Get<SelectionManager>();
        string toolButton = ToolButton.ActiveToolButton.Value?.ToString() ?? "<none>";
        string workingTool = InteractionManager.StateMachine.State.GetType().Name;
        int layerCount = document.Get<LayerTreeNode>().CountSubtreeNodes(LayerTreeChildIsAlive) - 1;

        return
            $$"""
            Document: {{settings.Name.Value}}
            Document path: {{RedactPath(settings.FilePath.Value)}}
            Modified: {{AppDocumentManager.WorkingDocumentModified}}
            Current frame: {{selection.CurrentFrame.Value}}
            Working layer: {{DescribeLayer(selection.WorkingLayer.Value)}}
            Selected layers: {{selection.SelectedLayers.Count}}
            Selected shapes: {{selection.SelectedShapes.Count}}
            Tool button: {{toolButton}}
            Working tool: {{workingTool}}
            Stroke brush: {{DescribeBrush(selection.WorkingStrokeBrush.Value)}}
            Vector fill brush: {{DescribeBrush(selection.WorkingVectorFillBrush.Value)}}
            Layer count: {{layerCount}}
            """;
    }

    private static bool LayerTreeChildIsAlive(Entity entity)
    {
        return !entity.IsNull && entity.IsAlive && entity.Has<LayerTreeNode>();
    }

    private static string CollectBreadcrumbs()
    {
        lock (Gate)
        {
            return Breadcrumbs.Count == 0
                ? "<none>"
                : string.Join(Environment.NewLine, Breadcrumbs);
        }
    }

    private static string ReadLogTail()
    {
        string path = LogFilePath;
        if (!File.Exists(path))
            return $"Log file not found: {path}";

        try
        {
            var lines = new Queue<string>();
            using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                lines.Enqueue(line);
                while (lines.Count > LogTailLines)
                    lines.Dequeue();
            }

            string result = string.Join(Environment.NewLine, lines);
            return result.Length <= LogTailChars
                ? result
                : result[^LogTailChars..];
        }
        catch (Exception exception)
        {
            return $"Could not read log file {path}: {exception.Message}";
        }
    }

    private static string CollectCpuName()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OS.GetProcessorName();

        try
        {
            var cpuNames = new List<string>();
            using var searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
            foreach (var item in searcher.Get())
            {
                var name = item["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name))
                    cpuNames.Add(name);
            }

            return cpuNames.Count == 0
                ? OS.GetProcessorName()
                : string.Join(" | ", cpuNames);
        }
        catch (Exception)
        {
            return OS.GetProcessorName();
        }
    }

    private static string SummarizeCommands(IReadOnlyList<ICommand> commands)
    {
        var parts = new List<string>();
        string previousName = null;
        int count = 0;

        foreach (var command in commands)
        {
            string name = command.GetType().Name;
            if (name == previousName)
            {
                count++;
                continue;
            }

            if (previousName != null)
                parts.Add(count == 1 ? previousName : $"{previousName}x{count}");

            previousName = name;
            count = 1;
        }

        if (previousName != null)
            parts.Add(count == 1 ? previousName : $"{previousName}x{count}");

        string result = string.Join(", ", parts.Take(24));
        if (parts.Count > 24)
            result += $", ... +{parts.Count - 24}";
        return result;
    }

    private static string DescribeLayerKind(Entity entity)
    {
        if (entity.Has<FolderLayerSetting>())
            return entity.Get<FolderLayerSetting>().IsCelFolder
                ? "Cel folder"
                : entity.Tagged<CelTag>() ? "Cel" : "Folder layer";
        if (entity.Has<ShapeLayerSetting>()) return "Shape layer";
        if (entity.Has<ImageLayerSetting>()) return "Image layer";
        if (entity.Has<VectorFillLayerSetting>()) return "Vector fill layer";
        return "Entity";
    }

    private static string DescribeBrush(Entity entity)
    {
        if (entity.IsNull) return "<none>";
        if (!entity.IsAlive) return $"<dead {entity.PackedValue}>";
        if (entity.Has<StrokeBrushSetting>())
            return $"Stroke \"{entity.Get<StrokeBrushSetting>().Name.Value}\"";
        if (entity.Has<FillBrushSetting>())
            return "Vector fill brush";
        return $"Entity {entity.PackedValue}";
    }

    private static string RedactPath(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + path[home.Length..]
            : path;
    }

    private static void AppendCodeBlock(StringBuilder builder, string text)
    {
        builder.AppendLine("```text");
        builder.AppendLine(text);
        builder.AppendLine("```");
    }
}
