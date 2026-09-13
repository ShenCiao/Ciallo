using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ObservableCollections;
using FileAccess = Godot.FileAccess;

namespace Ciallo.Data;

/// <summary>
/// App-level catalog of vector-fill marker textures, shared across all documents.
/// Built-in markers are regenerated from <c>res://</c> SVGs on every launch; user-imported
/// markers are persisted as PNG under <see cref="MarkerFolder"/>.
/// </summary>
/// <remarks>
/// The catalog only provides marker textures to pick from. Each fill brush keeps its own
/// inline copy of the chosen texture (see <see cref="FillBrushSetting.MarkerTexture"/>),
/// mirroring how documents hold their own brushes independent of <see cref="AppStrokeBrushLibrary"/>.
/// </remarks>
public static class AppMarkerTextureLibrary
{
    public sealed class MarkerEntry
    {
        public required ImageTexture Texture;
        public bool IsBuiltIn;
    }

    public static readonly ObservableList<MarkerEntry> Markers = [];

    public static readonly string MarkerFolder = "user://Marker/";

    // Built-in marker files are named Bullseye{outer}_{inner}.svg.
    // outer: 0 circle, 1 square, 2 diamond, 3 hexagon, 4 rounded square, 5 octagon.
    // inner: 0 dot, 1 crosshair, 2 ring, 3 diamond dot.
    private const int BuiltInOuterMarkerCount = 6;
    private const int BuiltInInnerMarkerCount = 4;

    /// <summary>
    /// Marker textures are white silhouettes tinted at draw time by <see cref="FillBrushSetting.MarkerColor"/>.
    /// Coerce every imported image to a 128x128 LA8 mask: luminance is discarded by the white-tint convention,
    /// only the alpha shape matters.
    /// </summary>
    public static void ConvertMarkerImage(Image img)
    {
        img.Convert(Image.Format.La8);
        const int side = 128;
        if (img.GetWidth() != side || img.GetHeight() != side)
            img.Resize(side, side);
    }

    public static void Initialise()
    {
        Markers.Clear();
        Markers.AddRange(CreateBuiltInMarkers());
        TryLoadUserMarkers();
    }

    private static List<MarkerEntry> CreateBuiltInMarkers()
    {
        var result = new List<MarkerEntry>();
        for (int outer = 0; outer < BuiltInOuterMarkerCount; outer++)
        {
            for (int inner = 0; inner < BuiltInInnerMarkerCount; inner++)
            {
                string path = $"res://Rendering/Image/Bullseye{outer}_{inner}.svg";
                var image = GD.Load<Image>(path);
                if (image == null)
                {
                    GD.PrintErr($"Cannot load built-in marker {path}.");
                    continue;
                }
                ConvertMarkerImage(image);
                result.Add(new MarkerEntry { Texture = ImageTexture.CreateFromImage(image), IsBuiltIn = true });
            }
        }
        return result;
    }

    public static void ResetBuiltInMarkers()
    {
        var userMarkers = Markers.Where(m => !m.IsBuiltIn).ToList();
        Markers.Clear();
        Markers.AddRange(CreateBuiltInMarkers());
        Markers.AddRange(userMarkers);
    }

    /// <summary>
    /// Imports an image as a new user marker, appends it to the catalog and returns its texture.
    /// Returns null if the image cannot be used.
    /// </summary>
    public static ImageTexture Import(Image image)
    {
        if (image == null || image.IsEmpty())
            return null;
        ConvertMarkerImage(image);
        image.GenerateMipmaps();
        var texture = ImageTexture.CreateFromImage(image);
        Markers.Add(new MarkerEntry { Texture = texture, IsBuiltIn = false });
        return texture;
    }

    private static void TryLoadUserMarkers()
    {
        if (AppCommandLineOptions.FactoryStartup) return;
        using var baseDir = DirAccess.Open("user://");
        if (baseDir == null || !baseDir.DirExists("Marker"))
            return;

        using var dir = DirAccess.Open(MarkerFolder);
        if (dir == null) return;

        var fileNames = new List<string>();
        dir.ListDirBegin();
        string entry;
        while ((entry = dir.GetNext()) != "")
            if (entry.EndsWith(".png"))
                fileNames.Add(entry);
        dir.ListDirEnd();

        // Files are saved as zero-padded indices; numeric/ordinal sort restores insertion order.
        fileNames.Sort(StringComparer.Ordinal);

        foreach (var name in fileNames)
        {
            var image = new Image();
            using var file = FileAccess.Open(MarkerFolder + name, FileAccess.ModeFlags.Read);
            if (file == null) continue;
            var error = image.LoadPngFromBuffer(file.GetBuffer((long)file.GetLength()));
            if (error != Error.Ok || image.IsEmpty()) continue;
            ConvertMarkerImage(image);
            image.GenerateMipmaps();
            Markers.Add(new MarkerEntry { Texture = ImageTexture.CreateFromImage(image), IsBuiltIn = false });
        }
    }

    public static void Save()
    {
        if (AppCommandLineOptions.FactoryStartup) return;
        using var baseDir = DirAccess.Open("user://");
        if (baseDir == null) return;
        if (!baseDir.DirExists("Marker"))
            baseDir.MakeDir("Marker");

        // Clear out previous user markers.
        using var dir = DirAccess.Open(MarkerFolder);
        if (dir == null) return;
        dir.ListDirBegin();
        string fileName;
        while ((fileName = dir.GetNext()) != "")
            if (fileName.EndsWith(".png"))
                dir.Remove(fileName);
        dir.ListDirEnd();

        int index = 0;
        foreach (var marker in Markers.Where(m => !m.IsBuiltIn))
        {
            var image = marker.Texture.GetImage();
            var path = MarkerFolder + index.ToString("D4") + ".png";
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null) continue;
            file.StoreBuffer(image.SavePngToBuffer());
            index++;
        }
    }
}
