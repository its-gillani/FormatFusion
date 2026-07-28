namespace FormatFusion.Core.Models;

/// <summary>Result of a smart compression job.</summary>
public record CompressResult(
    Guid JobId,
    bool Success,
    double TargetSizeMB,
    double AchievedSizeMB,
    double QualityUsed,           // JPEG Q-factor (1-100) or video CRF
    string ResolutionUsed,        // e.g. "3840x2160" or "1920x1080 (downscaled)"
    bool WasDownscaled,
    int IterationsUsed,
    TimeSpan Duration,
    string? WarningMessage = null,
    string? ErrorMessage = null)
{
    public double AccuracyPercent =>
        TargetSizeMB > 0
            ? Math.Round(Math.Abs(AchievedSizeMB - TargetSizeMB) / TargetSizeMB * 100, 1)
            : 0;

    public bool IsWithinTolerance => AccuracyPercent <= 5.0; // ±5% accepted
}

/// <summary>Fast pre-encode estimate returned by ISmartCompressor.EstimateOutputSizeAsync.</summary>
public record EstimateResult(
    double EstimatedSizeMB,
    double EstimatedBitrateKbps,    // Video only, 0 for photos
    string ResolutionWillBe,        // e.g. "3840x2160" or "1920x1080 (will downscale)"
    bool DownscaleRequired,
    EstimateQualityWarning Warning)
{
    public bool IsAchievable => Warning != EstimateQualityWarning.ImpossibleTooSmall;
}

public enum EstimateQualityWarning
{
    None,
    QualityWillBeReduced,       // Achievable but quality noticeably impacted
    DownscaleRequired,          // Must reduce resolution to hit target
    ImpossibleTooSmall          // Target is unreachable even at minimum quality/resolution
}
