namespace FormatFusion.Core.Models;

/// <summary>A single target-size compression request (photos and videos only).</summary>
public record CompressJob(
    Guid Id,
    string InputPath,
    string OutputPath,
    double TargetSizeMB,
    string OutputExtension)
{
    public static CompressJob Create(string inputPath, string outputPath, double targetMB, string outputExt)
        => new(Guid.NewGuid(), inputPath, outputPath, targetMB, outputExt);
}
