using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Ciallo.GuiControl;
using FFMpegCore;
using FFMpegCore.Pipes;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite]
public class ExportPngTests
{
    [TestCase]
    public void PremultipliedHdrPreservesDarkColorsAndLinearAlpha()
    {
        var pixels = HalfPixels(
            1f / 1024, 1f / 2048, 0, 1,
            0.5f, 0.125f, 0.25f, 0.5f,
            0.99072265625f, 0.456298828125f, 0.270263671875f, 1,
            1, 0.5f, 0.25f, 0,
            2, -0.5f, 0, 0.5f);

        ExportPngWriter.ConvertLinearRgbaHalfToSrgb16(pixels);

        AssertThat(MemoryMarshal.Cast<byte, ushort>(pixels).ToArray()).ContainsExactly(
            (ushort)827, (ushort)413, (ushort)0, (ushort)65535,
            (ushort)65535, (ushort)35199, (ushort)48192, (ushort)32768,
            (ushort)65267, (ushort)46255, (ushort)36480, (ushort)65535,
            (ushort)0, (ushort)0, (ushort)0, (ushort)0,
            (ushort)65535, (ushort)0, (ushort)0, (ushort)32768);
    }

    [TestCase]
    public async Task SequenceKeepsPng16ColorMetadataAlphaAndLiteralPercentPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ciallo-png-图像 50%-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var pattern = Path.Combine(directory.Replace("%", "%%"), "frame_%%_%04d.png");
            await ExportPngWriter.SaveFramesAsync(2, 1, pattern, 2, 7,
                i => Task.FromResult(i == 0
                    ? HalfPixels(1f / 1024, 0, 0, 1, 0.5f, 0.125f, 0.25f, 0.5f)
                    : HalfPixels(0, 0, 0, 0, 0.25f, 0.25f, 0.25f, 1)),
                BinaryDirectory()).WaitAsync(TimeSpan.FromSeconds(30));

            var first = Path.Combine(directory, "frame_%_0007.png");
            var second = Path.Combine(directory, "frame_%_0008.png");
            AssertThat(File.Exists(second)).IsTrue();
            var png = await File.ReadAllBytesAsync(first);
            AssertThat(png[24]).IsEqual((byte)16);
            AssertThat(png[25]).IsEqual((byte)6);
            var chunkNames = ReadChunkNames(png);
            AssertThat(chunkNames.Contains("sRGB")).IsTrue();
            AssertThat(chunkNames.Contains("gAMA")).IsTrue();
            AssertThat(chunkNames.Contains("cHRM")).IsTrue();

            using var raw = new MemoryStream();
            await FFMpegArguments.FromFileInput(first)
                .OutputToPipe(new StreamPipeSink(raw), options => options
                    .ForceFormat("rawvideo").ForcePixelFormat(BitConverter.IsLittleEndian ? "rgba64le" : "rgba64be"))
                .ProcessAsynchronously(ffMpegOptions: new FFOptions { BinaryFolder = BinaryDirectory() });
            AssertThat(MemoryMarshal.Cast<byte, ushort>(raw.ToArray()).ToArray()).ContainsExactly(
                (ushort)827, (ushort)0, (ushort)0, (ushort)65535,
                (ushort)65535, (ushort)35199, (ushort)48192, (ushort)32768);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase]
    public async Task EncoderFailureReleasesBlockedFrameProducer()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), "ciallo-missing-" + Guid.NewGuid().ToString("N"));
        Exception failure = null;
        try
        {
            await ExportPngWriter.SaveFramesAsync(1, 1, Path.Combine(missingDirectory, "%04d.png"), 100, 0,
                _ => Task.FromResult(HalfPixels(0, 0, 0, 1)), BinaryDirectory())
                .WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (Exception error)
        {
            failure = error;
        }
        AssertThat(failure).IsNotNull();
        AssertThat(failure is TimeoutException).IsFalse();
    }

    private static byte[] HalfPixels(params float[] components) =>
        MemoryMarshal.AsBytes(components.Select(value => (Half)value).ToArray().AsSpan()).ToArray();

    private static string[] ReadChunkNames(byte[] png)
    {
        var names = new System.Collections.Generic.List<string>();
        for (var offset = 8; offset < png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            names.Add(Encoding.ASCII.GetString(png, offset + 4, 4));
            offset += 12 + length;
        }
        return names.ToArray();
    }

    private static string BinaryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "Ciallo.csproj")))
            directory = directory.Parent!;
        return Path.Combine(directory.FullName, "ExternalData", "ffmpeg", RuntimeInformation.RuntimeIdentifier);
    }
}
