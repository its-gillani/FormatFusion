using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;

namespace FormatFusion.Core.Services;

public class HardwareCapabilities
{
    public bool IsNvidiaPresent { get; set; }
    public bool IsAmdPresent { get; set; }
    public bool IsIntelPresent { get; set; }
    
    public bool IsNvidiaAV1Supported { get; set; }
    public bool IsAmdAV1Supported { get; set; }
    public bool IsIntelAV1Supported { get; set; }

    public bool NvidiaUsable { get; set; }
    public bool AmdUsable { get; set; }
    public bool IntelUsable { get; set; }
}

public interface IHardwareDetectionService
{
    Task<HardwareCapabilities> DetectCapabilitiesAsync(string ffmpegExePath);
}

public class HardwareDetectionService : IHardwareDetectionService
{
    private HardwareCapabilities? _cachedCapabilities;

    public async Task<HardwareCapabilities> DetectCapabilitiesAsync(string ffmpegExePath)
    {
        if (_cachedCapabilities != null) return _cachedCapabilities;

        var caps = new HardwareCapabilities();

        // 1. Detect Hardware via WMI
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                {
                    caps.IsNvidiaPresent = true;
                    if (name.Contains("RTX 40") || name.Contains("RTX 50")) caps.IsNvidiaAV1Supported = true;
                }
                else if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                {
                    caps.IsAmdPresent = true;
                    if (name.Contains("RX 7") || name.Contains("RX 8") || name.Contains("7000") || name.Contains("8000")) caps.IsAmdAV1Supported = true;
                }
                else if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                {
                    caps.IsIntelPresent = true;
                    if (name.Contains("Arc") || name.Contains("Ultra")) caps.IsIntelAV1Supported = true;
                }
            }
        }
        catch { }

        Serilog.Log.Information($"[HardwareDetection] WMI Results: NvidiaPresent={caps.IsNvidiaPresent}, AmdPresent={caps.IsAmdPresent}, IntelPresent={caps.IsIntelPresent}");
        // 2. ffmpeg encoders check
        var ffmpegEncoders = await GetFfmpegEncodersAsync(ffmpegExePath);

        Serilog.Log.Information($"[HardwareDetection] FFmpeg encoders check complete. NV={ffmpegEncoders.Contains("h264_nvenc")}, AMD={ffmpegEncoders.Contains("h264_amf")}, Intel={ffmpegEncoders.Contains("h264_qsv")}");
        // 3. Trial Encodes for present and capable backends
        if (caps.IsNvidiaPresent && ffmpegEncoders.Contains("h264_nvenc"))
        {
            caps.NvidiaUsable = await TestEncoderAsync(ffmpegExePath, "h264_nvenc");
        }
        if (caps.IsAmdPresent && ffmpegEncoders.Contains("h264_amf"))
        {
            caps.AmdUsable = await TestEncoderAsync(ffmpegExePath, "h264_amf");
        }
        if (caps.IsIntelPresent && ffmpegEncoders.Contains("h264_qsv"))
        {
            caps.IntelUsable = await TestEncoderAsync(ffmpegExePath, "h264_qsv");
        }

        Serilog.Log.Information($"[HardwareDetection] Final usability: NvidiaUsable={caps.NvidiaUsable}, AmdUsable={caps.AmdUsable}, IntelUsable={caps.IntelUsable}");
        _cachedCapabilities = caps;
        return caps;
    }

    private async Task<HashSet<string>> GetFfmpegEncodersAsync(string ffmpegExePath)
    {
        var encoders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExePath,
                Arguments = "-encoders",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return encoders;
            string output = await process.StandardOutput.ReadToEndAsync();
            
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("V"))
                {
                    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        encoders.Add(parts[1]);
                    }
                }
            }
        }
        catch { }
        return encoders;
    }

    private async Task<bool> TestEncoderAsync(string ffmpegExePath, string codec)
    {
        try
        {
            var args = $"-y -f lavfi -i color=c=black:s=256x256:d=0.1 -c:v {codec} -f null -";
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Serilog.Log.Information($"[Trial Encode] Codec {codec} exit code {process.ExitCode}. Stderr: {stderr}");
            return process.ExitCode == 0 && !stderr.Contains("Error");
        }
        catch (Exception ex) { Serilog.Log.Error(ex, $"[Trial Encode] Exception for {codec}"); return false; }
    }
}

