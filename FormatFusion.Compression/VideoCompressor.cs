using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using FFMpegCore;
using FFMpegCore.Enums; // Speed enum

namespace FormatFusion.Compression;

/// <summary>
/// Compresses a video toward a user-specified target size using
/// 2-pass VBR encoding with automatic bitrate calculation.
/// Retries up to 3 times adjusting bitrate, then falls back to resolution downscale.
/// </summary>
public sealed class VideoCompressor : ISmartCompressor
{
    private const double TolerancePct = 5.0;       // ±5% is accepted
    private const double SafetyMargin = 0.97;       // Target 97% of requested to avoid overshooting
    private const int MaxRetries = 3;
    private const int MinVideoBitrateKbps = 100;

    // Standard downscale ladder (width only; height scales proportionally)
    private static readonly int[] DownscaleLadder = { 3840, 1920, 1280, 854, 640 };

    private readonly FormatFusion.Core.Services.AppSettings _settings;

    public VideoCompressor(FormatFusion.Core.Services.AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<EstimateResult> EstimateOutputSizeAsync(
        string inputPath,
        double targetMB,
        CancellationToken cancellationToken = default)
    {
        var info = await FFProbe.AnalyseAsync(inputPath, cancellationToken: cancellationToken);
        var durationSec = info.Duration.TotalSeconds;

        if (durationSec <= 0)
            return new EstimateResult(0, 0, "Unknown", false, EstimateQualityWarning.ImpossibleTooSmall);

        const int audioBitrateKbps = 128;
        double targetBits = targetMB * 1_048_576.0 * 8 * SafetyMargin;
        double videoBitrateKbps = (targetBits / durationSec / 1000.0) - audioBitrateKbps;

        var videoStream = info.VideoStreams.FirstOrDefault();
        int origWidth = videoStream?.Width ?? 1920;
        int origHeight = videoStream?.Height ?? 1080;

        bool downscaleRequired = videoBitrateKbps < MinVideoBitrateKbps;
        var warning = EstimateQualityWarning.None;
        string resolution = $"{origWidth}x{origHeight}";

        if (videoBitrateKbps < 0)
        {
            warning = EstimateQualityWarning.ImpossibleTooSmall;
        }
        else if (downscaleRequired)
        {
            warning = EstimateQualityWarning.DownscaleRequired;
            // Find the best downscale step
            var targetWidth = DownscaleLadder.FirstOrDefault(w => w < origWidth);
            if (targetWidth > 0)
            {
                int targetHeight = (int)(origHeight * ((double)targetWidth / origWidth));
                resolution = $"{targetWidth}x{targetHeight} (will downscale)";
                // Recalculate bitrate at lower resolution — same formula
                videoBitrateKbps = (targetBits / durationSec / 1000.0) - audioBitrateKbps;
            }
        }
        else if (videoBitrateKbps < 300)
        {
            warning = EstimateQualityWarning.QualityWillBeReduced;
        }

        double estimatedMB = ((videoBitrateKbps + audioBitrateKbps) * 1000.0 * durationSec) / 8.0 / 1_048_576.0;

        return new EstimateResult(
            EstimatedSizeMB: Math.Round(estimatedMB, 1),
            EstimatedBitrateKbps: Math.Max(0, Math.Round(videoBitrateKbps, 0)),
            ResolutionWillBe: resolution,
            DownscaleRequired: downscaleRequired,
            Warning: warning);
    }

    public async Task<CompressResult> CompressAsync(
        CompressJob job,
        IProgress<JobProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;

        var info = await FFProbe.AnalyseAsync(job.InputPath, cancellationToken: cancellationToken);
        var durationSec = info.Duration.TotalSeconds;
        var videoStream = info.VideoStreams.FirstOrDefault();
        int origWidth = videoStream?.Width ?? 1920;

        const int audioBitrateKbps = 128;
        double targetBits = job.TargetSizeMB * 1_048_576.0 * 8 * SafetyMargin;
        double videoBitrateKbps = (targetBits / durationSec / 1000.0) - audioBitrateKbps;

        bool wasDownscaled = false;
        string resolutionUsed = $"{videoStream?.Width}x{videoStream?.Height}";
        int scaledWidth = 0;

        if (videoBitrateKbps < MinVideoBitrateKbps)
        {
            // Need to downscale — pick the next step down
            scaledWidth = DownscaleLadder.FirstOrDefault(w => w < origWidth);
            if (scaledWidth == 0) scaledWidth = 640; // Floor
            wasDownscaled = true;
            resolutionUsed = $"{scaledWidth}x? (downscaled)";
            // Recalculate with same bitrate target
        }

        Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);

        // Up to MaxRetries encode attempts
        double actualMB = 0;
        string? errorMsg = null;
        
        string passLogPrefix = Path.Combine(Path.GetTempPath(), "FormatFusion", $"passlog_{job.Id}");
        Directory.CreateDirectory(Path.GetDirectoryName(passLogPrefix)!);
        string dummyOutputFile = passLogPrefix + "_dummy.mp4";

        bool tryHardware = _settings.HardwareBackend != "CPU";

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vbr = (int)Math.Max(MinVideoBitrateKbps, videoBitrateKbps);
            bool hwSuccess = false;

            if (tryHardware)
            {
                try
                {
                    var backend = _settings.HardwareBackend;
                    var desiredCodec = _settings.VideoCodec;
                    
                    var codec = desiredCodec switch
                    {
                        "H.265" => backend switch { "NVENC" => "hevc_nvenc", "AMF" => "hevc_amf", "QSV" => "hevc_qsv", _ => "libx265" },
                        "VP9" => backend switch { "QSV" => "vp9_qsv", _ => "libvpx-vp9" },
                        "AV1" => backend switch { "NVENC" => "av1_nvenc", "AMF" => "av1_amf", "QSV" => "av1_qsv", _ => "libsvtav1" },
                        _ => backend switch { "NVENC" => "h264_nvenc", "AMF" => "h264_amf", "QSV" => "h264_qsv", _ => "libx264" }
                    };
                    var rcArgs = backend switch
                    {
                        "NVENC" => "-rc cbr",
                        "AMF" => "-rc cbr",
                        "QSV" => "-b:v",  // QSV usually uses -b:v natively via standard FFMpegCore, but we can pass it
                        _ => ""
                    };

                    progress.Report(new JobProgress(job.Id, 
                        5 + (attempt - 1) * 28.0, $"Encoding (attempt {attempt}, {backend})", null));
                        
                    var passStarted = DateTime.UtcNow;
                    await FFMpegArguments
                        .FromFileInput(job.InputPath)
                        .OutputToFile(job.OutputPath, overwrite: true, opts =>
                        {
                            opts.WithVideoCodec(codec);
                            if (!string.IsNullOrEmpty(rcArgs) && rcArgs != "-b:v")
                                opts.WithCustomArgument(rcArgs);
                            opts.WithVideoBitrate(vbr);
                            opts.WithAudioCodec("aac");
                            opts.WithAudioBitrate(128);
                            if (wasDownscaled && scaledWidth > 0)
                                opts.WithCustomArgument($"-vf scale={scaledWidth}:-2");
                        })
                        .NotifyOnProgress(p =>
                        {
                            var baseOffset = 5 + (attempt - 1) * 28.0;
                            var pct = baseOffset + (p / 100.0 * 28);
                            
                            TimeSpan? eta = null;
                            if (p > 0.5)
                            {
                                var elapsed = (DateTime.UtcNow - passStarted).TotalSeconds;
                                var totalEst = elapsed / (p / 100.0);
                                eta = TimeSpan.FromSeconds(Math.Max(0, totalEst - elapsed));
                            }
                            
                            progress.Report(new JobProgress(job.Id, Math.Min(pct, 95), "Encoding (GPU)", eta));
                        }, info.Duration)
                        .CancellableThrough(cancellationToken)
                        .ProcessAsynchronously(throwOnError: true);
                        
                    hwSuccess = true;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    tryHardware = false;
                    progress.Report(new JobProgress(job.Id, 5, $"{_settings.HardwareBackend} failed ({ex.Message}), falling back to CPU", null));
                    if (File.Exists(job.OutputPath)) File.Delete(job.OutputPath);
                }
            }

            if (!hwSuccess)
            {
                progress.Report(new JobProgress(job.Id,
                    5 + (attempt - 1) * 28.0, $"Pass 1/2 (attempt {attempt}, CPU)", null));

                var fallbackCodec = _settings.VideoCodec switch
                {
                    "H.265" => "libx265",
                    "VP9" => "libvpx-vp9",
                    "AV1" => "libsvtav1",
                    _ => "libx264"
                };

                // Pass 1
                await FFMpegArguments
                    .FromFileInput(job.InputPath)
                    .OutputToFile(dummyOutputFile, overwrite: true, opts =>
                    {
                        opts.WithVideoCodec(fallbackCodec);
                        opts.WithVideoBitrate(vbr);
                        opts.WithCustomArgument("-pass 1");
                        opts.WithCustomArgument($"-passlogfile \"{passLogPrefix}\"");
                        opts.WithCustomArgument("-an");
                        opts.ForceFormat("null");
                        if (wasDownscaled && scaledWidth > 0)
                            opts.WithCustomArgument($"-vf scale={scaledWidth}:-2");
                    })
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously(throwOnError: true);

                progress.Report(new JobProgress(job.Id,
                    5 + (attempt - 1) * 28.0 + 14, $"Pass 2/2 (attempt {attempt}, CPU)", null));

                var passStarted = DateTime.UtcNow;
                // Pass 2
                await FFMpegArguments
                    .FromFileInput(job.InputPath)
                    .OutputToFile(job.OutputPath, overwrite: true, opts =>
                    {
                        opts.WithVideoCodec(fallbackCodec);
                        opts.WithVideoBitrate(vbr);
                        opts.WithCustomArgument("-pass 2");
                        opts.WithCustomArgument($"-passlogfile \"{passLogPrefix}\"");
                        opts.WithAudioCodec("aac");
                        opts.WithAudioBitrate(128);
                        if (wasDownscaled && scaledWidth > 0)
                            opts.WithCustomArgument($"-vf scale={scaledWidth}:-2");
                    })
                    .NotifyOnProgress(p =>
                    {
                        var baseOffset = 5 + (attempt - 1) * 28.0 + 14;
                        var pct = baseOffset + (p / 100.0 * 14);
                        
                        TimeSpan? eta = null;
                        if (p > 0.5)
                        {
                            var elapsed = (DateTime.UtcNow - passStarted).TotalSeconds;
                            var totalEst = elapsed / (p / 100.0);
                            eta = TimeSpan.FromSeconds(Math.Max(0, totalEst - elapsed));
                        }
                        
                        progress.Report(new JobProgress(job.Id, Math.Min(pct, 95), "Encoding (CPU)", eta));
                    }, info.Duration)
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously(throwOnError: true);
            }

            actualMB = new FileInfo(job.OutputPath).Length / 1_048_576.0;
            var diff = (actualMB - job.TargetSizeMB) / job.TargetSizeMB * 100;

            if (Math.Abs(diff) <= TolerancePct) break; // ✅ Within tolerance

            // Adjust bitrate for next attempt
            videoBitrateKbps *= job.TargetSizeMB / actualMB;
        }

        progress.Report(new JobProgress(job.Id, 100, "Done", TimeSpan.Zero));

        try
        {
            if (File.Exists(dummyOutputFile)) File.Delete(dummyOutputFile);
            var log1 = passLogPrefix + "-0.log";
            var log2 = passLogPrefix + "-0.log.mbtree";
            if (File.Exists(log1)) File.Delete(log1);
            if (File.Exists(log2)) File.Delete(log2);
        }
        catch { /* best effort */ }

        return new CompressResult(
            JobId: job.Id,
            Success: errorMsg is null,
            TargetSizeMB: job.TargetSizeMB,
            AchievedSizeMB: Math.Round(actualMB, 2),
            QualityUsed: videoBitrateKbps,
            ResolutionUsed: resolutionUsed,
            WasDownscaled: wasDownscaled,
            IterationsUsed: MaxRetries,
            Duration: DateTime.UtcNow - started,
            WarningMessage: wasDownscaled ? "Resolution reduced to meet target." : null,
            ErrorMessage: errorMsg);
    }
}
