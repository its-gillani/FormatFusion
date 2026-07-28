using FormatFusion.Core.Models;

namespace FormatFusion.Core.Interfaces;

/// <summary>
/// Smart compressor for photos and videos — targets a concrete file size in MB.
/// </summary>
public interface ISmartCompressor
{
    /// <summary>
    /// Fast path: estimate output size without a full encode.
    /// For photos uses thumbnail extrapolation; for video uses bitrate formula.
    /// Returns approximate output size in MB.
    /// </summary>
    Task<EstimateResult> EstimateOutputSizeAsync(
        string inputPath,
        double targetMB,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Full compression. Iterates toward target via binary search (photo)
    /// or 2-pass VBR (video). Reports progress throughout.
    /// </summary>
    Task<CompressResult> CompressAsync(
        CompressJob job,
        IProgress<JobProgress> progress,
        CancellationToken cancellationToken = default);
}
