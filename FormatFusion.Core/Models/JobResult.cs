namespace FormatFusion.Core.Models;

/// <summary>Result of a completed conversion or compression job.</summary>
public record JobResult(
    Guid JobId,
    bool Success,
    string InputPath,
    string OutputPath,
    long InputSizeBytes,
    long OutputSizeBytes,
    TimeSpan Duration,
    string? ErrorMessage = null,
    bool Cancelled = false,
    string BackendUsed = "CPU")
{
    public double SavingsPercent =>
        InputSizeBytes > 0
            ? Math.Round(((double)OutputSizeBytes - InputSizeBytes) / InputSizeBytes * 100, 1)
            : 0;

    public string InputSizeFormatted => FormatBytes(InputSizeBytes);
    public string OutputSizeFormatted => FormatBytes(OutputSizeBytes);

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{bytes / 1_024.0:F1} KB",
        _ => $"{bytes} B"
    };
}
