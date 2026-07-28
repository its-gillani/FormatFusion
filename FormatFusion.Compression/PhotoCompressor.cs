using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using ImageMagick;

namespace FormatFusion.Compression;

/// <summary>
/// Compresses a photo toward a user-specified target file size using
/// a binary search on the JPEG/WebP quality parameter, with resolution
/// fallback if quality alone can't reach the target.
/// </summary>
public sealed class PhotoCompressor : ISmartCompressor
{
    private const double TolerancePercent = 5.0;   // ±5% is accepted as a match
    private const int MaxIterations = 12;

    public async Task<EstimateResult> EstimateOutputSizeAsync(
        string inputPath,
        double targetMB,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var image = new MagickImage(inputPath);
            var originalBytes = new FileInfo(inputPath).Length;
            var originalMB = originalBytes / 1_048_576.0;

            // Fast estimate: encode a 10% thumbnail, extrapolate
            using var thumbnail = image.Clone();
            thumbnail.Resize((uint)(image.Width / 10), (uint)(image.Height / 10));
            using var ms = new MemoryStream();
            thumbnail.Quality = 85;
            thumbnail.Write(ms, MagickFormat.Jpg);
            var thumbnailBytes = ms.Length;

            // Extrapolate: full image ≈ thumbnail × (fullPixels / thumbPixels)
            double pixelRatio = (double)(image.Width * image.Height) /
                                ((image.Width / 10) * (image.Height / 10));
            double estimatedBytes = thumbnailBytes * pixelRatio * 0.85; // 0.85 correction factor
            double estimatedMB = estimatedBytes / 1_048_576.0;

            double targetBytes = targetMB * 1_048_576.0;
            bool downscaleRequired = estimatedBytes * 0.1 > targetBytes; // Even Q=1 won't be enough

            var warning = EstimateQualityWarning.None;
            if (downscaleRequired) warning = EstimateQualityWarning.DownscaleRequired;
            else if (targetMB < originalMB * 0.3) warning = EstimateQualityWarning.QualityWillBeReduced;

            if (targetMB < 0.01) warning = EstimateQualityWarning.ImpossibleTooSmall;

            return new EstimateResult(
                EstimatedSizeMB: Math.Round(estimatedMB, 2),
                EstimatedBitrateKbps: 0,
                ResolutionWillBe: $"{image.Width}x{image.Height}" + (downscaleRequired ? " (will downscale)" : ""),
                DownscaleRequired: downscaleRequired,
                Warning: warning);

        }, cancellationToken);
    }

    public async Task<CompressResult> CompressAsync(
        CompressJob job,
        IProgress<JobProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;

        return await Task.Run(() =>
        {
            using var image = new MagickImage(job.InputPath);
            var targetBytes = (long)(job.TargetSizeMB * 1_048_576.0);

            progress.Report(new JobProgress(job.Id, 10, "Starting compression", null));

            var (achievedBytes, quality, wasDownscaled, resolution, iters) =
                BinarySearchQuality(image, targetBytes, progress, job.Id, cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);
            image.Quality = (uint)quality;
            image.Write(job.OutputPath);

            var achievedMB = achievedBytes / 1_048_576.0;
            var accuracy = Math.Abs(achievedMB - job.TargetSizeMB) / job.TargetSizeMB * 100;

            string? warning = null;
            if (wasDownscaled) warning = "Resolution was reduced to meet target size.";
            else if (quality < 40) warning = "High compression applied — image quality is significantly reduced.";

            return new CompressResult(
                JobId: job.Id,
                Success: true,
                TargetSizeMB: job.TargetSizeMB,
                AchievedSizeMB: Math.Round(achievedMB, 2),
                QualityUsed: quality,
                ResolutionUsed: resolution,
                WasDownscaled: wasDownscaled,
                IterationsUsed: iters,
                Duration: DateTime.UtcNow - started,
                WarningMessage: warning);
        }, cancellationToken);
    }

    /// <summary>
    /// Binary search on quality (1–95). Falls back to half-resolution if Q=1 still overshoots.
    /// Returns (achievedBytes, qualityUsed, wasDownscaled, resolutionString, iterationCount).
    /// </summary>
    private static (long bytes, int quality, bool downscaled, string resolution, int iters)
        BinarySearchQuality(
            MagickImage image,
            long targetBytes,
            IProgress<JobProgress> progress,
            Guid jobId,
            CancellationToken ct)
    {
        int lo = 1, hi = 95, quality = 85;
        bool wasDownscaled = false;
        string resolution = $"{image.Width}x{image.Height}";
        int iteration = 0;
        long lastBytes = 0;

        // Try original resolution first
        for (iteration = 1; iteration <= MaxIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            quality = (lo + hi) / 2;
            image.Quality = (uint)quality;

            using var ms = new MemoryStream();
            image.Write(ms, MagickFormat.Jpg);
            lastBytes = ms.Length;

            double pct = 10 + (iteration / (double)MaxIterations) * 70;
            progress.Report(new JobProgress(jobId, pct, $"Iteration {iteration}/{MaxIterations}", null));

            var diff = (lastBytes - targetBytes) / (double)targetBytes * 100;

            if (Math.Abs(diff) <= TolerancePercent) break; // Within tolerance

            if (lastBytes > targetBytes) hi = quality - 1;
            else lo = quality + 1;

            if (lo > hi) break;
        }

        // Fallback: if still over target at Q=1, halve resolution and retry once
        if (lastBytes > targetBytes * 1.05 && quality <= 5)
        {
            image.Resize(image.Width / 2, image.Height / 2);
            resolution = $"{image.Width}x{image.Height} (downscaled)";
            wasDownscaled = true;
            lo = 1; hi = 95;

            for (int i2 = 1; i2 <= MaxIterations; i2++)
            {
                ct.ThrowIfCancellationRequested();
                quality = (lo + hi) / 2;
                image.Quality = (uint)quality;

                using var ms2 = new MemoryStream();
                image.Write(ms2, MagickFormat.Jpg);
                lastBytes = ms2.Length;

                double pct = 80 + (i2 / (double)MaxIterations) * 15;
                progress.Report(new JobProgress(jobId, pct, $"Retry at lower resolution", null));

                var diff = (lastBytes - targetBytes) / (double)targetBytes * 100;
                if (Math.Abs(diff) <= TolerancePercent) break;
                if (lastBytes > targetBytes) hi = quality - 1;
                else lo = quality + 1;
                if (lo > hi) break;
            }
        }

        progress.Report(new JobProgress(jobId, 100, "Done", TimeSpan.Zero));
        return (lastBytes, quality, wasDownscaled, resolution, iteration);
    }
}
