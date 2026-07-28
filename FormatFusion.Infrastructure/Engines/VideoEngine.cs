using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using FormatFusion.Core.Services;
using FFMpegCore;
using FFMpegCore.Enums;
using System.IO;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Serilog;
using FormatFusion.Infrastructure.Services;

namespace FormatFusion.Infrastructure.Engines;

public sealed class VideoEngine : IFormatConverter
{
    private readonly AppSettings _settings;
    private readonly IUserPromptService _promptService;
    private readonly HardwareAccelerationResolver _hwResolver;

    public VideoEngine(AppSettings settings, IUserPromptService promptService, HardwareAccelerationResolver hwResolver)
    {
        _settings = settings;
        _promptService = promptService;
        _hwResolver = hwResolver;
    }

    public IReadOnlyList<string> SupportedInputExtensions { get; } = new[]
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".wmv", ".3gp", ".gif"
    };

    public IReadOnlyList<string> SupportedOutputExtensions { get; } = new[]
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".wmv", ".3gp", ".gif"
    };

    public async Task<JobResult> ConvertAsync(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        long inputSize = new FileInfo(job.InputPath).Length;
        string targetExt = job.TargetExtension.ToLowerInvariant();
        string desiredVideoCodec = job.Options.VideoCodec;

        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(job.InputPath, cancellationToken: cancellationToken);
            var videoStream = mediaInfo.PrimaryVideoStream;
            var audioStream = mediaInfo.PrimaryAudioStream;

            string sourceVideoCodec = videoStream?.CodecName ?? "none";
            string sourceAudioCodec = audioStream?.CodecName ?? "none";

            bool isVideoCompatible = desiredVideoCodec == "Default" && IsVideoCodecCompatible(sourceVideoCodec, targetExt);
            bool isAudioCompatible = IsAudioCodecCompatible(sourceAudioCodec, targetExt);

            if (desiredVideoCodec == "Default" && !isVideoCompatible)
            {
                desiredVideoCodec = "FORCE_FALLBACK";
            }

            string finalAudioCodec = isAudioCompatible ? "copy" : GetFallbackAudioCodec(targetExt);
            var duration = mediaInfo.Duration;

            bool hwAttemptedAndFailed = false;
            string hwErrorSummary = string.Empty;

            var hwResult = _hwResolver.Resolve(targetExt, desiredVideoCodec);
            bool hwSuccess = false;

            if (hwResult.UseHardware)
            {
                try
                {
                    string hwArgs = HardwareAccelerationResolver.GetHwAccelArgs(hwResult.ResolvedBackend);
                    string rcArgs = HardwareAccelerationResolver.GetRateControlArgs(hwResult.ResolvedBackend);
                    string pixFmt = HardwareAccelerationResolver.GetPixFmtArgs(hwResult.ResolvedBackend);

                    string progressPhase = hwResult.WasAuto ? $"Encoding video (Auto \u2192 {hwResult.ResolvedBackend})" : $"Encoding video ({hwResult.ResolvedBackend})";
                    progress.Report(new JobProgress(job.Id, 5, progressPhase, null));
                    var passStarted = DateTime.UtcNow;
                    var stderr = new StringBuilder();

                    var processor = FFMpegArguments
                        .FromFileInput(job.InputPath, verifyExists: true, options =>
                        {
                            if (!string.IsNullOrEmpty(hwArgs))
                                options.WithCustomArgument(hwArgs);
                        })
                        .OutputToFile(job.OutputPath, overwrite: job.OverwriteExisting, opts =>
                        {
                            opts.WithCustomArgument(pixFmt);
                            opts.WithVideoCodec(hwResult.HwCodecName);
                            if (!string.IsNullOrEmpty(rcArgs))
                                opts.WithCustomArgument(rcArgs);
                            
                            opts.WithAudioCodec(finalAudioCodec);
                            if (finalAudioCodec != "copy") opts.WithAudioBitrate(128);

                            if (!job.Options.PreserveMetadata)
                                opts.WithCustomArgument("-map_metadata -1");
                        });

                    Serilog.Log.Information($"[VideoEngine HW Pass] Invoking FFmpeg with arguments: {processor.Arguments}");

                    await processor
                        .CancellableThrough(cancellationToken)
                        .NotifyOnProgress(p =>
                        {
                            var percent = Math.Min(99, p);
                            TimeSpan? eta = null;
                            if (p > 0.5)
                            {
                                var elapsed = (DateTime.UtcNow - passStarted).TotalSeconds;
                                var totalEst = elapsed / (p / 100.0);
                                eta = TimeSpan.FromSeconds(Math.Max(0, totalEst - elapsed));
                            }
                            progress.Report(new JobProgress(job.Id, percent, progressPhase, eta));
                        }, duration)
                        .NotifyOnError(line => stderr.AppendLine(line))
                        .ProcessAsynchronously(throwOnError: false);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        TryDeleteOutput(job.OutputPath);
                        throw new OperationCanceledException(cancellationToken);
                    }

                    if (stderr.ToString().Contains("Error"))
                    {
                        throw new Exception($"FFmpeg error:\n{stderr}");
                    }
                    hwSuccess = true;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    hwAttemptedAndFailed = true;
                    hwErrorSummary = ex.Message;
                    
                    if (ex.Message.Contains("FFmpeg error:"))
                    {
                        var lines = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        hwErrorSummary = lines.LastOrDefault(l => (l.Contains("Error") || l.Contains("failed") || l.Contains("Unknown encoder") || l.Contains("Impossible to convert")) && !l.Contains("Conversion failed!")) 
                                         ?? lines.LastOrDefault() 
                                         ?? "Unknown FFmpeg error";
                    }

                    Log.Warning(ex, "[{JobId}] Hardware backend {Backend} failed: {Reason}", job.Id, hwResult.ResolvedBackend, hwErrorSummary);
                    hwSuccess = false;
                    TryDeleteOutput(job.OutputPath);

                    if (!hwResult.WasAuto)
                    {
                        if (!_promptService.PromptUser($"The hardware encoder ({hwResult.ResolvedBackend}) failed during conversion:\n{hwErrorSummary}\n\nDo you want to fall back to software (CPU) encoding?", "Hardware Encoder Failed"))
                        {
                            throw new OperationCanceledException("User cancelled after hardware encoder failure.");
                        }
                    }
                }
            }

            if (!hwSuccess)
            {
                var passStarted = DateTime.UtcNow;
                var codec = GetVideoCodecName(targetExt, desiredVideoCodec);
                
                string phaseName = hwAttemptedAndFailed ? $"Encoding video (CPU Fallback: {hwErrorSummary})" : "Encoding video (CPU)";
                progress.Report(new JobProgress(job.Id, 5, phaseName, null));

                var stderr = new StringBuilder();
                var processorSw = FFMpegArguments
                    .FromFileInput(job.InputPath)
                    .OutputToFile(job.OutputPath, overwrite: job.OverwriteExisting, opts =>
                    {
                        opts.WithVideoCodec(codec);
                        opts.WithAudioCodec(finalAudioCodec);
                        if (finalAudioCodec != "copy") opts.WithAudioBitrate(128);
                        
                        if (!job.Options.PreserveMetadata)
                            opts.WithCustomArgument("-map_metadata -1");
                    });

                Serilog.Log.Information($"[VideoEngine SW Pass] Invoking FFmpeg with arguments: {processorSw.Arguments}");

                await processorSw
                    .NotifyOnProgress(p =>
                    {
                        var percent = Math.Min(99, p);
                        TimeSpan? eta = null;
                        if (codec != "copy" && p > 0.5)
                        {
                            var elapsed = (DateTime.UtcNow - passStarted).TotalSeconds;
                            var totalEst = elapsed / (p / 100.0);
                            eta = TimeSpan.FromSeconds(Math.Max(0, totalEst - elapsed));
                        }
                        progress.Report(new JobProgress(job.Id, percent, phaseName, eta));
                    }, duration)
                    .NotifyOnError(line => stderr.AppendLine(line))
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously(throwOnError: false);
                    
                if (cancellationToken.IsCancellationRequested)
                {
                    TryDeleteOutput(job.OutputPath);
                    throw new OperationCanceledException(cancellationToken);
                }
                    
                if (stderr.ToString().Contains("Error"))
                {
                    throw new Exception($"FFmpeg software encoding error: {stderr}");
                }
            }

            progress.Report(new JobProgress(job.Id, 100, "Done", TimeSpan.Zero));
            var outputSize = new FileInfo(job.OutputPath).Length;
            return new JobResult(job.Id, true, job.InputPath, job.OutputPath,
                inputSize, outputSize, DateTime.UtcNow - started, BackendUsed: hwSuccess ? hwResult.ResolvedBackend : "CPU");
        }
        catch (OperationCanceledException)
        {
            TryDeleteOutput(job.OutputPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDeleteOutput(job.OutputPath);
            return new JobResult(job.Id, false, job.InputPath, job.OutputPath,
                inputSize, 0, DateTime.UtcNow - started, ex.Message);
        }
    }

    private static string GetVideoCodecName(string ext, string userCodec)
    {
        if (ext == ".gif") return "gif";
        return userCodec switch
        {
            "Default" => "copy",
            "H.264" => "libx264",
            "H.265" => "libx265",
            "VP9" => "libvpx-vp9",
            "AV1" => "libsvtav1",
            "FORCE_FALLBACK" => GetFallbackVideoCodec(ext),
            _ => "copy"
        };
    }

    private static string GetFallbackVideoCodec(string ext)
    {
        return ext switch
        {
            ".webm" => "libvpx-vp9",
            ".avi" => "mpeg4",
            ".wmv" => "wmv2",
            ".flv" => "libx264",
            ".3gp" => "mpeg4",
            ".mp4" or ".mkv" or ".mov" => "libx264",
            _ => "libx264"
        };
    }

    private static bool IsVideoCodecCompatible(string sourceCodec, string targetExt)
    {
        sourceCodec = sourceCodec.ToLowerInvariant();
        if (sourceCodec.StartsWith("lib")) sourceCodec = sourceCodec.Substring(3);
        if (sourceCodec.Contains("vpx-")) sourceCodec = sourceCodec.Replace("vpx-", "");
        if (sourceCodec.Contains("svt")) sourceCodec = sourceCodec.Replace("svt", "");

        return targetExt switch
        {
            ".webm" => sourceCodec is "vp8" or "vp9" or "av1",
            ".mp4" or ".mov" => sourceCodec is "h264" or "hevc" or "h265" or "av1" or "mpeg4" or "mpeg2video",
            ".mkv" => sourceCodec is "h264" or "hevc" or "h265" or "vp8" or "vp9" or "av1" or "mpeg4" or "mpeg2video" or "theora",
            ".avi" => sourceCodec is "mpeg4" or "msmpeg4v3" or "msmpeg4v2" or "mjpeg",
            ".flv" => sourceCodec is "flv1" or "vp6f" or "h264",
            ".wmv" => sourceCodec is "wmv1" or "wmv2" or "wmv3" or "vc1",
            ".3gp" => sourceCodec is "h263" or "mpeg4" or "h264",
            ".gif" => sourceCodec is "gif",
            _ => false
        };
    }

    private static bool IsAudioCodecCompatible(string sourceCodec, string targetExt)
    {
        if (targetExt == ".gif") return false;

        sourceCodec = sourceCodec.ToLowerInvariant();
        return targetExt switch
        {
            ".webm" => sourceCodec is "vorbis" or "opus",
            ".mp4" or ".mov" => sourceCodec is "aac" or "mp3" or "ac3" or "eac3" or "alac" or "flac",
            ".mkv" => sourceCodec is "aac" or "mp3" or "ac3" or "eac3" or "vorbis" or "opus" or "flac" or "pcm_s16le",
            ".avi" => sourceCodec is "mp3" or "ac3" or "pcm_s16le",
            ".flv" => sourceCodec is "mp3" or "aac",
            ".wmv" => sourceCodec is "wmav1" or "wmav2",
            ".3gp" => sourceCodec is "aac" or "amr_nb" or "amr_wb",
            _ => false
        };
    }

    private static string GetFallbackAudioCodec(string targetExt)
    {
        return targetExt switch
        {
            ".webm" => "libopus",
            ".avi" => "mp3",
            ".flv" => "mp3",
            ".wmv" => "wmav2",
            ".3gp" => "aac",
            ".gif" => "none",
            _ => "aac"
        };
    }

    private static void TryDeleteOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

