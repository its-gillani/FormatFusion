using FormatFusion.Core;
using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using System.Diagnostics;
using System.Text;

namespace FormatFusion.Infrastructure.Engines;

/// <summary>
/// Document conversion engine backed by Pandoc (external binary).
/// Pandoc is bundled in the Tools\ directory of the app or auto-detected from PATH.
/// </summary>
public sealed class DocumentEngine : IFormatConverter
{
    private readonly string _pandocPath;

    public IReadOnlyList<string> SupportedInputExtensions { get; } = new[]
    {
        ".pdf", ".docx", ".txt", ".rtf", ".odt", ".epub", ".html", ".md"
    };

    public IReadOnlyList<string> SupportedOutputExtensions { get; } = new[]
    {
        ".docx", ".txt", ".rtf", ".odt", ".epub", ".html", ".md", ".pdf"
    };

    public DocumentEngine(string? pandocPath = null)
    {
        _pandocPath = pandocPath ?? FindPandoc();
    }

    public async Task<JobResult> ConvertAsync(
        ConversionJob job,
        IProgress<JobProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var inputSize = new FileInfo(job.InputPath).Length;

        if (!File.Exists(_pandocPath))
            return new JobResult(job.Id, false, job.InputPath, job.OutputPath, inputSize, 0,
                TimeSpan.Zero, "Pandoc not found. Please install Pandoc from https://pandoc.org/installing.html");

        try
        {
            progress.Report(new JobProgress(job.Id, 10, "Converting document", null));

            Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);

            var targetExt = job.TargetExtension.ToLowerInvariant().TrimStart('.');
            var args = BuildPandocArgs(job.InputPath, job.OutputPath, targetExt);

            var stderr = new StringBuilder();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pandocPath,
                    Arguments = args,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            process.Start();
            process.BeginErrorReadLine();

            // Poll for cancellation since Pandoc doesn't expose per-frame progress
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var waitTask = process.WaitForExitAsync(cts.Token);

            // Simulate progress ticks while waiting
            _ = Task.Run(async () =>
            {
                int tick = 20;
                while (!waitTask.IsCompleted && tick < 95)
                {
                    await Task.Delay(500, cancellationToken);
                    tick = Math.Min(95, tick + 5);
                    progress.Report(new JobProgress(job.Id, tick, "Converting document", null));
                }
            }, cancellationToken);

            await waitTask;

            if (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                catch (Exception) { }

                TryDeleteOutput(job.OutputPath);
                throw new OperationCanceledException();
            }

            if (process.ExitCode != 0)
            {
                TryDeleteOutput(job.OutputPath);
                return new JobResult(job.Id, false, job.InputPath, job.OutputPath,
                    inputSize, 0, DateTime.UtcNow - started,
                    $"Pandoc error: {stderr}");
            }

            progress.Report(new JobProgress(job.Id, 100, "Done", TimeSpan.Zero));
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
    }

    private static string BuildPandocArgs(string input, string output, string targetExt)
    {
        // Map extension to Pandoc format name
        var fmt = targetExt switch
        {
            "docx" => "docx",
            "odt" => "odt",
            "txt" => "plain",
            "rtf" => "rtf",
            "epub" => "epub",
            "html" => "html",
            "md" => "markdown",
            "pdf" => "pdf",
            _ => targetExt
        };

        return $"\"{input}\" -o \"{output}\" --to={fmt} --standalone";
    }

    private static string FindPandoc()
    {
        // 1. App-bundled binary
        var bundled = Path.Combine(AppContext.BaseDirectory, "Tools", "pandoc.exe");
        if (File.Exists(bundled)) return bundled;

        // 2. System PATH
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir.Trim(), "pandoc.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return bundled; // Return expected path even if missing — engine will report friendly error
    }

    private static void TryDeleteOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
