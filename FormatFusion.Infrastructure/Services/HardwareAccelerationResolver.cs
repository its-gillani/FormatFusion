using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using FormatFusion.Core.Services;
using System;

namespace FormatFusion.Infrastructure.Services;

public class HardwareAccelerationResolver
{
    private readonly AppSettings _settings;
    private readonly IUserPromptService _promptService;

    public HardwareAccelerationResolver(AppSettings settings, IUserPromptService promptService)
    {
        _settings = settings;
        _promptService = promptService;
    }

    public HardwareResolutionResult Resolve(string targetExt, string desiredVideoCodec)
    {
        Serilog.Log.Information($"[HW Resolver] Request: TargetExt={targetExt}, Codec={desiredVideoCodec}, InitialBackend={_settings.HardwareBackend}");
        Serilog.Log.Information($"[HW Resolver] Cached Caps: NvidiaUsable={_settings.HardwareCaps?.NvidiaUsable}, AmdUsable={_settings.HardwareCaps?.AmdUsable}, IntelUsable={_settings.HardwareCaps?.IntelUsable}");
        string resolvedBackend = "CPU";
        var initialBackend = _settings.HardwareBackend;

        if (desiredVideoCodec == "Default" || desiredVideoCodec == "FORCE_FALLBACK")
        {
            desiredVideoCodec = targetExt.ToLower() switch
            {
                ".mp4" or ".mkv" or ".mov" or ".flv" => "H.264",
                ".heic" or ".heif" => "H.265",
                ".webm" => "VP9",
                _ => "Default"
            };
        }

        if (initialBackend == "Auto (Recommended)")
        {
            if (_settings.HardwareCaps?.NvidiaUsable == true) resolvedBackend = "NVIDIA GPU";
            else if (_settings.HardwareCaps?.AmdUsable == true) resolvedBackend = "AMD GPU";
            else if (_settings.HardwareCaps?.IntelUsable == true) resolvedBackend = "Intel GPU";
            else resolvedBackend = "CPU";
        }
        else
        {
            resolvedBackend = initialBackend;
            bool isUsable = resolvedBackend switch
            {
                "NVIDIA GPU" => _settings.HardwareCaps?.NvidiaUsable == true,
                "AMD GPU" => _settings.HardwareCaps?.AmdUsable == true,
                "Intel GPU" => _settings.HardwareCaps?.IntelUsable == true,
                _ => true
            };

            if (!isUsable)
            {
                if (!_promptService.PromptUser($"{resolvedBackend} is not available on this system or failed initialization. We must use the CPU (Software) encoder instead. Proceed?", "Format Conflict"))
                {
                    throw new OperationCanceledException("User cancelled due to hardware encoder conflict.");
                }
                resolvedBackend = "CPU";
            }
        }

        bool useHardware = resolvedBackend != "CPU" && desiredVideoCodec != "Default";

        if (resolvedBackend != "CPU")
        {
            if (desiredVideoCodec == "VP9")
            {
                if (initialBackend != "Auto (Recommended)")
                {
                    if (!_promptService.PromptUser($"VP9 encoding is not supported by {resolvedBackend}. We must use the CPU (Software) encoder instead. Proceed?", "Format Conflict"))
                        throw new OperationCanceledException("User cancelled due to codec conflict.");
                }
                useHardware = false;
                resolvedBackend = "CPU";
            }
            else if (desiredVideoCodec == "AV1")
            {
                bool av1Supported = resolvedBackend switch
                {
                    "NVIDIA GPU" => _settings.HardwareCaps?.IsNvidiaAV1Supported == true,
                    "AMD GPU" => _settings.HardwareCaps?.IsAmdAV1Supported == true,
                    "Intel GPU" => _settings.HardwareCaps?.IsIntelAV1Supported == true,
                    _ => false
                };

                if (!av1Supported)
                {
                    if (initialBackend != "Auto (Recommended)")
                    {
                        if (!_promptService.PromptUser($"AV1 encoding is not supported by your {resolvedBackend} hardware generation. We must use the CPU encoder instead. Proceed?", "Format Conflict"))
                            throw new OperationCanceledException("User cancelled due to codec conflict.");
                    }
                    useHardware = false;
                    resolvedBackend = "CPU";
                }
            }
        }

        string? hwCodec = null;
        if (useHardware)
        {
            hwCodec = desiredVideoCodec switch
            {
                "H.264" => resolvedBackend switch { "NVIDIA GPU" => "h264_nvenc", "AMD GPU" => "h264_amf", "Intel GPU" => "h264_qsv", _ => null },
                "H.265" => resolvedBackend switch { "NVIDIA GPU" => "hevc_nvenc", "AMD GPU" => "hevc_amf", "Intel GPU" => "hevc_qsv", _ => null },
                "AV1" => resolvedBackend switch { "NVIDIA GPU" => "av1_nvenc", "AMD GPU" => "av1_amf", "Intel GPU" => "av1_qsv", _ => null },
                _ => null
            };
            if (hwCodec == null)
            {
                useHardware = false;
                resolvedBackend = "CPU";
            }
        }

        Serilog.Log.Information($"[HW Resolver] Final Result: UseHardware={useHardware}, ResolvedBackend={resolvedBackend}, HwCodec={hwCodec}");
        return new HardwareResolutionResult(useHardware, resolvedBackend, hwCodec, initialBackend == "Auto (Recommended)");
    }

    public static string GetHwAccelArgs(string resolvedBackend) => resolvedBackend switch
    {
        "NVIDIA GPU" => "-hwaccel cuda",
        "AMD GPU" => "-hwaccel d3d11va",
        "Intel GPU" => "-hwaccel qsv",
        _ => ""
    };

    public static string GetRateControlArgs(string resolvedBackend) => resolvedBackend switch
    {
        "NVIDIA GPU" => "-rc constqp -qp 22",
        "AMD GPU" => "-rc cqp -qp_i 22 -qp_p 22",
        "Intel GPU" => "-global_quality 22",
        _ => ""
    };

    public static string GetPixFmtArgs(string resolvedBackend) => resolvedBackend switch
    {
        "NVIDIA GPU" => "-pix_fmt p010le",
        "AMD GPU" => "-pix_fmt nv12",
        "Intel GPU" => "-pix_fmt nv12",
        _ => ""
    };
}

public record HardwareResolutionResult(bool UseHardware, string ResolvedBackend, string? HwCodecName, bool WasAuto);
