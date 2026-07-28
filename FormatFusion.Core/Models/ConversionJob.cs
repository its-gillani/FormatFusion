namespace FormatFusion.Core.Models;
using System;

/// <summary>A single file conversion request.</summary>
public record ConversionJob(
    Guid Id,
    string InputPath,
    string OutputPath,
    string TargetExtension,
    ConversionOptions Options,
    bool OverwriteExisting = false)
{
    public static ConversionJob Create(string inputPath, string outputPath, string targetExt,
        ConversionOptions? options = null, bool overwrite = false)
        => new(Guid.NewGuid(), inputPath, outputPath, targetExt, options ?? new ConversionOptions(), overwrite);
}

/// <summary>Per-job conversion options. Extend as needed per engine.</summary>
public record ConversionOptions(
    int? ImageQuality = null,         // JPEG/WebP quality 1-100
    bool PreserveMetadata = true,
    string? AdditionalArgs = null,    // Pass-through for advanced users
    string? VideoCodec = null,        // Specific video codec for this job (e.g. H.264, VP9), overrides AppSettings
    int? MaxImageWidth = null,
    int? MaxImageHeight = null
);
