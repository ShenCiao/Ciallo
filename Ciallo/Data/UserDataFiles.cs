using System;
using System.IO;
using Godot;

namespace Ciallo.Data;

internal static class UserDataFiles
{
    internal static void WriteAtomic(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                       System.IO.FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(true);
            }
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    internal static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GD.PrintErr($"Cannot remove user data '{path}': {exception.Message}");
        }
    }
}
