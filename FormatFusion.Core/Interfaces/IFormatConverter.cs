using FormatFusion.Core.Models;

namespace FormatFusion.Core.Interfaces;

/// <summary>
/// All format conversion engines implement this contract.
/// The FormatRegistry resolves the correct engine by file extension.
/// </summary>
public interface IFormatConverter
{
    /// <summary>File extensions this engine can read (e.g. ".jpg", ".png").</summary>
    IReadOnlyList<string> SupportedInputExtensions { get; }

    /// <summary>File extensions this engine can write (e.g. ".webp", ".bmp").</summary>
    IReadOnlyList<string> SupportedOutputExtensions { get; }

    /// <summary>
    /// Convert a single file. Must be called on a background thread.
    /// Reports progress via <paramref name="progress"/> (thread-safe).
    /// </summary>
    Task<JobResult> ConvertAsync(
        ConversionJob job,
        IProgress<JobProgress> progress,
        CancellationToken cancellationToken = default);
}
