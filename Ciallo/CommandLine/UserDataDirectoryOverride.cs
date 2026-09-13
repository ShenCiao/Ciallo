using System;
using System.IO;
using Godot;

namespace Ciallo.CommandLine;

internal static class UserDataDirectoryOverride
{
    public static void Apply(string path)
    {
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath(path)));
        string customName = Path.GetFileName(target);
        string parent = Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(customName))
            throw new ArgumentException("--user-data-dir must name a directory below a filesystem root.");
        switch (OS.GetName())
        {
            case "Windows":
                // Godot reevaluates APPDATA whenever it resolves user:// on Windows.
                OS.SetEnvironment("APPDATA", parent);
                break;
            case "Linux":
            case "FreeBSD":
            case "NetBSD":
            case "OpenBSD":
                OS.SetEnvironment("XDG_DATA_HOME", parent);
                break;
            default:
                // Other desktop platforms expose only a custom name below their data directory.
                customName = Path.GetRelativePath(OS.GetDataDir(), target).Replace('\\', '/');
                if (customName == "." || Path.IsPathRooted(customName) || customName.Contains("..", StringComparison.Ordinal))
                    throw new PlatformNotSupportedException($"On {OS.GetName()}, --user-data-dir must be below '{OS.GetDataDir()}'.");
                break;
        }

        // Let Godot append its custom directory name to the platform data path.
        // Never save these process-specific settings to project.godot.
        ProjectSettings.SetSetting("application/config/use_custom_user_dir", true);
        ProjectSettings.SetSetting("application/config/custom_user_dir_name", customName);
        string actual = Path.TrimEndingDirectorySeparator(Path.GetFullPath(OS.GetUserDataDir()));
        var comparison = OS.GetName() == "Windows" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(actual, target, comparison))
            throw new ArgumentException($"Godot resolved --user-data-dir to '{actual}' instead of '{target}'.");

        var error = DirAccess.MakeDirRecursiveAbsolute("user://");
        if (error != Error.Ok)
            throw new IOException($"Cannot create user data directory '{actual}': {error}.");
        using var directory = DirAccess.Open("user://");
        if (directory == null)
            throw new IOException($"Cannot access user data directory '{actual}': {DirAccess.GetOpenError()}.");
    }
}
