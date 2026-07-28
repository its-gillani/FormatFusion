using FormatFusion.Core;
using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace FormatFusion.Infrastructure.Engines;

/// <summary>
/// Archive engine backed by SharpCompress.
/// Supports repackaging between ZIP, 7Z, TAR, GZ archive formats.
/// RAR extraction supported; RAR creation excluded (license requirement).
/// </summary>
public sealed class ArchiveEngine : IFormatConverter
{
    public IReadOnlyList<string> SupportedInputExtensions { get; } = new[]
    {
        ".zip", ".7z", ".tar", ".gz", ".bz2", ".rar", ".xz"
    };

    public IReadOnlyList<string> SupportedOutputExtensions { get; } = new[]
    {
        ".zip", ".7z", ".tar", ".gz"
        // RAR write excluded — WinRAR license required
    };

    public async Task<JobResult> ConvertAsync(
        ConversionJob job,
        IProgress<JobProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var inputSize = new FileInfo(job.InputPath).Length;
        var tempExtractDir = Path.Combine(Path.GetTempPath(), "FormatFusion", Guid.NewGuid().ToString("N"));

        try
        {
            progress.Report(new JobProgress(job.Id, 5, "Reading archive", null));

            await Task.Run(() =>
            {
                Directory.CreateDirectory(tempExtractDir);
                Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);

                // Extract all entries to temp directory
                using (var archive = ArchiveFactory.Open(job.InputPath))
                {
                    var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                    int done = 0;
                    foreach (var entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        entry.WriteToDirectory(tempExtractDir,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                        done++;
                        var pct = 10 + (done / (double)entries.Count) * 50;
                        progress.Report(new JobProgress(job.Id, pct, "Extracting", null));
                    }
                }

                progress.Report(new JobProgress(job.Id, 65, "Repacking", null));

                // Repack into target format
                var targetExt = job.TargetExtension.ToLowerInvariant().TrimStart('.');
                RepackDirectory(tempExtractDir, job.OutputPath, targetExt, progress, job.Id, cancellationToken);

                progress.Report(new JobProgress(job.Id, 100, "Done", TimeSpan.Zero));

            }, cancellationToken);

            var outputSize = new FileInfo(job.OutputPath).Length;
            return new JobResult(job.Id, true, job.InputPath, job.OutputPath,
                inputSize, outputSize, DateTime.UtcNow - started, BackendUsed: "CPU");
        }
        catch (OperationCanceledException)
        {
            TryDeleteOutput(job.OutputPath);
            throw;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                TryDeleteOutput(job.OutputPath);
                throw new OperationCanceledException(cancellationToken);
            }
            TryDeleteOutput(job.OutputPath);
            return new JobResult(job.Id, false, job.InputPath, job.OutputPath,
                inputSize, 0, DateTime.UtcNow - started, ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempExtractDir);
        }
    }

    private static void RepackDirectory(string sourceDir, string outputPath, string targetExt,
        IProgress<JobProgress> progress, Guid jobId, CancellationToken ct)
    {
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        int done = 0;

        // WriterFactory approach — API-stable across SharpCompress versions
        var (archType, compType) = targetExt switch
        {
            "zip" => (ArchiveType.Zip,      CompressionType.Deflate),
            "7z"  => (ArchiveType.SevenZip, CompressionType.LZMA),
            "gz"  => (ArchiveType.Tar,      CompressionType.GZip),
            "tar" => (ArchiveType.Tar,      CompressionType.None),
            _     => (ArchiveType.Zip,      CompressionType.Deflate),
        };

        using var outStream = File.Create(outputPath);
        using var writer = WriterFactory.Open(outStream, archType,
            new WriterOptions(compType) { LeaveStreamOpen = false });

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var entryName = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
            writer.Write(entryName, file);
            done++;
            var pct = 65 + (double)done / files.Length * 34; // 65-99%
            progress.Report(new JobProgress(jobId, Math.Min(99, pct),
                $"Packing {done}/{files.Length}", null));
        }
    }

    private static void TryDeleteOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}
