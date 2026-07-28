using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using ImageMagick;
using FFMpegCore;
using FFMpegCore.Enums;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Serilog;
using FormatFusion.Infrastructure.Services;

namespace FormatFusion.Infrastructure.Engines;

public sealed class ImageEngine : IFormatConverter
{
    private readonly HardwareAccelerationResolver _hwResolver;

    public ImageEngine(HardwareAccelerationResolver hwResolver)
    {
        _hwResolver = hwResolver;
    }

    public IReadOnlyList<string> SupportedInputExtensions { get; } = new[]
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif",
        ".bmp", ".gif", ".tiff", ".tif", ".ico",
        ".cr2", ".cr3", ".nef", ".arw", ".dng"
    };

    public IReadOnlyList<string> SupportedOutputExtensions { get; } = new[]
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tiff", ".tif", ".ico"
    };

    public async Task<JobResult> ConvertAsync(ConversionJob job, IProgress<JobProgress> progress, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        long inputSize = new FileInfo(job.InputPath).Length;
        string targetExt = job.TargetExtension.ToLowerInvariant();

        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                progress.Report(new JobProgress(job.Id, 10, "Reading image", null));

                using var image = new MagickImage(job.InputPath);
                ct.ThrowIfCancellationRequested();

                if (job.Options.MaxImageWidth.HasValue && job.Options.MaxImageHeight.HasValue)
                {
                    if (image.Width > job.Options.MaxImageWidth.Value || image.Height > job.Options.MaxImageHeight.Value)
                    {
                        progress.Report(new JobProgress(job.Id, 20, "Resizing image", null));
                        var size = new MagickGeometry((uint)job.Options.MaxImageWidth.Value, (uint)job.Options.MaxImageHeight.Value)
                        {
                            IgnoreAspectRatio = false
                        };
                        image.Resize(size);
                    }
                }

                var format = GetMagickFormat(targetExt);
                image.Format = format;

                progress.Report(new JobProgress(job.Id, 50, "Writing image", null));
                image.Write(job.OutputPath);

                long outputSize = new FileInfo(job.OutputPath).Length;
                return new JobResult(job.Id, true, job.InputPath, job.OutputPath, inputSize, outputSize, DateTime.UtcNow - started);
            }, ct);
        }
        catch (OperationCanceledException)
        {
            TryDeleteOutput(job.OutputPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDeleteOutput(job.OutputPath);
            string errorMsg = ex.Message;
            if (ex is MagickException magickEx)
            {
                if (errorMsg.Contains("width or height exceeds limit"))
                    errorMsg = "Image dimensions exceed the maximum allowed by the output format.";
                else
                    errorMsg = $"Image processing failed: {magickEx.Message.Split('@').FirstOrDefault()?.Trim()}";
            }
            return new JobResult(job.Id, false, job.InputPath, job.OutputPath, inputSize, 0, DateTime.UtcNow - started, errorMsg);
        }
    }

    private static MagickFormat GetMagickFormat(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => MagickFormat.Jpeg,
            ".png" => MagickFormat.Png,
            ".webp" => MagickFormat.WebP,
            ".bmp" => MagickFormat.Bmp,
            ".gif" => MagickFormat.Gif,
            ".tiff" or ".tif" => MagickFormat.Tiff,
            ".ico" => MagickFormat.Ico,
            ".heic" => MagickFormat.Heic,
            ".heif" => MagickFormat.Heif,
            _ => throw new NotSupportedException($"Format {extension} not supported")
        };

    private static void TryDeleteOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}





