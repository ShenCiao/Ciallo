using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegLogLevel = FFMpegCore.Enums.FFMpegLogLevel;
using FFMpegCore.Pipes;
using Godot;

namespace Ciallo.GuiControl;

internal static class ExportPngWriter
{
    public static byte[] ReadHdrPixels(SubViewport viewport)
    {
        using var image = viewport.GetTexture().GetImage();
        return image.GetData();
    }

    public static string GetBinaryDirectory()
    {
        // Only the editor has real files behind res://. Exported executables
        // are deployed outside the PCK by the Ciallo export plugin.
        if (OS.HasFeature("editor"))
            return ProjectSettings.GlobalizePath($"res://ExternalData/ffmpeg/{RuntimeInformation.RuntimeIdentifier}");

        var executableDirectory = OS.GetExecutablePath().GetBaseDir();
        return Path.GetFullPath(Path.Combine(executableDirectory,
            OperatingSystem.IsMacOS() ? "../Helpers/ffmpeg" : "ffmpeg"));
    }

    public static Task SaveAsync(SubViewport viewport, string path)
    {
        var pixels = ReadHdrPixels(viewport);
        return SaveFramesAsync(viewport.Size.X, viewport.Size.Y, path, 1, 0,
            _ => Task.FromResult(pixels), GetBinaryDirectory(), singleImage: true);
    }

    internal static async Task SaveFramesAsync(int width, int height, string outputPath,
        int frameCount, int startNumber, Func<int, Task<byte[]>> renderFrame,
        string binaryDirectory, bool singleImage = false)
    {
        if (frameCount == 0)
            return;

        using var cancellation = new CancellationTokenSource();
        var frames = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
        });
        var source = new HdrFrameSource(frames.Reader, width, height);
        var encoder = Task.Run(async () =>
        {
            try
            {
                await FFMpegArguments.FromPipeInput(source, options => options.WithCustomArgument(
                        "-color_primaries bt709 -color_trc iec61966-2-1 -colorspace rgb -color_range pc"))
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options.WithVideoCodec("png").ForcePixelFormat("rgba64be")
                            .ForceFormat("image2").WithFrameOutputCount(frameCount);
                        if (singleImage)
                            options.WithCustomArgument("-update 1");
                        else
                            options.WithStartNumber(startNumber);
                    })
                    .WithLogLevel(FFMpegLogLevel.Error)
                    .CancellableThrough(cancellation.Token)
                    .ProcessAsynchronously(ffMpegOptions: new FFOptions { BinaryFolder = binaryDirectory });
            }
            finally
            {
                // Wake a producer blocked on a full queue if FFmpeg exits early.
                cancellation.Cancel();
            }
        });

        try
        {
            for (var i = 0; i < frameCount; i++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                // The caller stays on Godot's main thread; conversion and pipe
                // writes happen on the encoder worker with a bounded queue.
                var pixels = await renderFrame(i);
                await frames.Writer.WriteAsync(pixels, cancellation.Token);
            }
            frames.Writer.Complete();
            await encoder;
        }
        catch (Exception error)
        {
            cancellation.Cancel();
            frames.Writer.TryComplete();
            try
            {
                await encoder;
            }
            catch (Exception encoderError) when (error is OperationCanceledException)
            {
                throw new IOException($"PNG export failed: {encoderError.Message}", encoderError);
            }
            catch
            {
                // Preserve a render/file failure while awaiting process cleanup.
            }
            throw;
        }
    }

    internal static void ConvertLinearRgbaHalfToSrgb16(Span<byte> pixels)
    {
        var linear = MemoryMarshal.Cast<byte, Half>(pixels);
        var srgb = MemoryMarshal.Cast<byte, ushort>(pixels);
        for (var i = 0; i < linear.Length; i += 4)
        {
            // Read all components before overwriting the half-float bytes.
            var r = (float)linear[i];
            var g = (float)linear[i + 1];
            var b = (float)linear[i + 2];
            var a = (float)linear[i + 3];
            if (a <= 0)
            {
                srgb.Slice(i, 4).Clear();
                continue;
            }

            srgb[i] = EncodeSrgb(r / a);
            srgb[i + 1] = EncodeSrgb(g / a);
            srgb[i + 2] = EncodeSrgb(b / a);
            srgb[i + 3] = Quantize(a);
        }
    }

    private static ushort EncodeSrgb(float linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        return Quantize(linear <= 0.0031308f
            ? 12.92f * linear
            : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f);
    }

    private static ushort Quantize(float value) =>
        (ushort)MathF.Round(Math.Clamp(value, 0, 1) * ushort.MaxValue, MidpointRounding.AwayFromZero);

    private sealed class HdrFrameSource(ChannelReader<byte[]> frames, int width, int height) : IPipeSource
    {
        public string GetStreamArguments() =>
            $"-f rawvideo -pixel_format {(BitConverter.IsLittleEndian ? "rgba64le" : "rgba64be")} -video_size {width}x{height} -framerate 1";

        public async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
        {
            await foreach (var pixels in frames.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                ConvertLinearRgbaHalfToSrgb16(pixels);
                await outputStream.WriteAsync(pixels, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
