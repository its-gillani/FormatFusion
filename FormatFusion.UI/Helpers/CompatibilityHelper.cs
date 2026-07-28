using FormatFusion.Core.Services;
using FormatFusion.Core.Services;
using FormatFusion.UI.ViewModels;
using System.Windows.Media;

namespace FormatFusion.UI.Helpers;

public static class CompatibilityHelper
{
    // The user requested: a small "no-entry"/slash-circle icon for structural, and a small "hardware-swap" icon for hardware.
    public const string IconSlashCircle = "M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z M4.929 4.929l14.142 14.142";
    public const string IconHardwareSwap = "M17 3l4 4-4 4 M21 7H8a4 4 0 0 0-4 4v1 M7 21l-4-4 4-4 M3 17h13a4 4 0 0 0 4-4v-1"; 

    public static Brush GetStructuralConflictBrush() => System.Windows.Application.Current.TryFindResource("ThemeStructuralConflictBrush") as Brush ?? Brushes.Red;
    public static Brush GetHardwareConflictBrush() => System.Windows.Application.Current.TryFindResource("ThemeHardwareConflictBrush") as Brush ?? Brushes.Orange;
    public static Brush GetDefaultTextBrush() => System.Windows.Application.Current.TryFindResource("ThemeTextPrimaryBrush") as Brush ?? Brushes.White;

    public static bool IsCodecSupportedByContainer(string codec, string targetExt)
    {
        targetExt = targetExt.ToLowerInvariant();
        if (!targetExt.StartsWith('.')) targetExt = "." + targetExt;

        if (codec == "Default" || codec == "Global Change") return true;

        if (targetExt == ".webm") return codec is "VP9" or "AV1";
        if (targetExt == ".flv") return codec is "H.264";
        if (targetExt is ".mp4" or ".mov" or ".mkv") return codec is "H.264" or "H.265" or "VP9" or "AV1";
        if (targetExt is ".heic" or ".heif") return codec is "H.265";
        
        return true;
    }

    public static bool IsCodecSupportedByBackend(string codec, string resolvedBackend, HardwareCapabilities? caps)
    {
        if (codec == "Default" || codec == "Global Change" || resolvedBackend == "CPU" || resolvedBackend == "Auto (Recommended)") return true;

        if (codec == "VP9") return resolvedBackend == "Intel GPU";
        if (codec == "AV1")
        {
            return resolvedBackend switch
            {
                "NVIDIA GPU" => caps?.IsNvidiaAV1Supported == true,
                "AMD GPU" => caps?.IsAmdAV1Supported == true,
                "Intel GPU" => caps?.IsIntelAV1Supported == true,
                _ => false
            };
        }

        return true;
    }

    public static void EvaluateCodecOption(CodecOptionViewModel opt, string targetExt, string backend, HardwareCapabilities? caps)
    {
        if (opt.CodecName == "Global Change")
        {
            opt.TextColor = GetDefaultTextBrush();
            opt.ShowIcon = false;
            opt.WarningMessage = "";
            return;
        }

        bool isStructuralCompatible = IsCodecSupportedByContainer(opt.CodecName, targetExt);
        if (!isStructuralCompatible)
        {
            opt.TextColor = GetStructuralConflictBrush();
            opt.IconGeometry = IconSlashCircle;
            opt.ShowIcon = true;
            opt.WarningMessage = "Unsupported Combination";
            return;
        }

        var activeBackend = backend;
        if (activeBackend == "Auto (Recommended)")
        {
            if (caps?.NvidiaUsable == true) activeBackend = "NVIDIA GPU";
            else if (caps?.AmdUsable == true) activeBackend = "AMD GPU";
            else if (caps?.IntelUsable == true) activeBackend = "Intel GPU";
            else activeBackend = "CPU";
        }

        bool codecSupported = IsCodecSupportedByBackend(opt.CodecName, activeBackend, caps);
        if (!codecSupported && backend != "Auto (Recommended)")
        {
            opt.TextColor = GetHardwareConflictBrush();
            opt.IconGeometry = IconHardwareSwap;
            opt.ShowIcon = true;
            opt.WarningMessage = "Not supported by selected HWA";
            return;
        }

        opt.TextColor = GetDefaultTextBrush();
        opt.ShowIcon = false;
        opt.WarningMessage = "";
    }
}




