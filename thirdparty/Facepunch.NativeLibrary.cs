using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Steamworks;

internal static class FacepunchNativeLibrary
{
    // Godot loads managed assemblies from memory, so their Location cannot be
    // used to find the native library copied beside the application's output.
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", Justification = "Ciallo's binding assembly must register native resolution before any P/Invoke, including when loaded from memory.")]
    internal static void RegisterResolver()
        => NativeLibrary.SetDllImportResolver(typeof(SteamClient).Assembly, Resolve);

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != Platform.LibraryName)
            return IntPtr.Zero;

        var fileName =
            OperatingSystem.IsWindows() ? "steam_api64.dll" :
            OperatingSystem.IsMacOS() ? "libsteam_api.dylib" :
            "libsteam_api.so";
        return NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, fileName));
    }
}
