using FormatFusion.Core;
using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using FFMpegCore;
using FFMpegCore.Enums;

namespace FormatFusion.Infrastructure.Engines;

/// <summary>
/// Audio conversion engine backed by FFMpegCore (LGPL FFmpeg binary).
/// Supports: MP3, WAV, FLAC, AAC, OGG, OPUS, M4A.
/// </summary>
public sealed class AudioEngine : IFormatConverter
{
    public IReadOnlyList<string> SupportedInputExtensions { get; } = new[]
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".opus", ".m4a"
    };

    public IReadOnlyList<string> SupportedOutputExtensions { get; } = new[]
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".opus", ".m4a"
    };

    public async Task<JobResult> ConvertAsync(
        ConversionJob job,
        IProgress<JobProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var inputSize = new FileInfo(job.InputPath).Length;

        try
        {
            progress.Report(new JobProgress(job.Id, 5, "Analysing audio", null));

            var mediaInfo = await FFProbe.AnalyseAsync(job.InputPath, cancellationToken: cancellationToken);
            var duration = mediaInfo.Duration;

            Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);

            // Build FFmpeg args based on target format
            var targetExt = job.TargetExtension.ToLowerInvariant().TrimStart('.');

            await FFMpegArguments
                .FromFileInput(job.InputPath)
                .OutputToFile(job.OutputPath, overwrite: true, opts =>
                {
                    var codecName = GetCodecName(targetExt);
                    var bitrate = GetBitrateKbps(targetExt);
                    opts.WithAudioCodec(codecName);
                    opts.WithAudioBitrate(bitrate);
                    if (!job.Options.PreserveMetadata)
                        opts.WithCustomArgument("-map_metadata -1");
                })
                .NotifyOnProgress(p =>
                {
                    // p is a double 0-100 (percentage) when totalTimeSpan overload is used
                    var percent = Math.Min(99, p);
                    var etaSec = duration.TotalSeconds > 0 && p > 0
                        ? duration.TotalSeconds * (1 - p / 100.0)
                        : (double?)null;
                    var eta = etaSec.HasValue ? TimeSpan.FromSeconds(etaSec.Value) : (TimeSpan?)null;
                    progress.Report(new JobProgress(job.Id, percent, "Encoding audio", eta));
                }, duration)
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously(throwOnError: true);

            progress.Report(new JobProgress(job.Id, 100, "Done", TimeSpan.Zero));
            var outputSize = new FileInfo(job.OutputPath).Length;
            return new JobResult(job.Id, true, job.InputPath, job.OutputPath,
                inputSize, outputSize, DateTime.UtcNow - started, BackendUsed: "CPU");
        }
        catch (OperationCanceledException)
        {
            TryDeleteOutput(job.OutputPath);
            throw;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                TryDeleteOutput(job.OutputPath);
                throw new OperationCanceledException(cancellationToken);
            }
            TryDeleteOutput(job.OutputPath);
            return new JobResult(job.Id, false, job.InputPath, job.OutputPath,
                inputSize, 0, DateTime.UtcNow - started, ex.Message);
        }
    }

    private static string GetCodecName(string ext) => ext switch
    {
        "mp3" => "libmp3lame",
        "aac" or "m4a" => "aac",
        "ogg" => "libvorbis",
        "opus" => "libopus",
        "flac" => "flac",
        "wav" => "pcm_s16le",
        _ => "aac"
    };

    private static int GetBitrateKbps(string ext) => ext switch
    {
        "flac" or "wav" => 320,
        "mp3" or "aac" or "m4a" => 192,
        "ogg" or "opus" => 160,
        _ => 192
    };

    private static void TryDeleteOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
