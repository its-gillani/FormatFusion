using FormatFusion.Core;
using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using System.Threading.Channels;

namespace FormatFusion.Core.Services;

/// <summary>
/// Central job dispatcher. Uses a bounded Channel&lt;T&gt; as a queue.
/// Supports configurable parallelism, pause/resume, and per-job cancellation.
/// All progress events are marshalled back to the calling (UI) thread via
/// the SynchronizationContext captured at construction time.
/// </summary>
public sealed class JobOrchestrator : IJobOrchestrator, IDisposable
{
    private readonly IFormatRegistry _registry;
    private readonly ISmartCompressor? _photoCompressor;
    private readonly ISmartCompressor? _videoCompressor;
    private readonly SynchronizationContext? _uiContext;
    private readonly Channel<QueuedJob> _channel;
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = new();
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly List<Task> _workers = new();
    private bool _paused;
    private bool _disposed;

    public int MaxConcurrentJobs { get; }
    public bool IsPaused => _paused;
    public int ActiveJobCount => MaxConcurrentJobs - _concurrencySemaphore.CurrentCount;

    public event EventHandler<JobProgressEventArgs>? JobProgressChanged;
    public event EventHandler<JobCompletedEventArgs>? JobCompleted;
    public event EventHandler<JobEnqueuedEventArgs>? JobEnqueued;

    public JobOrchestrator(
        IFormatRegistry registry,
        int maxConcurrentJobs = 2,
        ISmartCompressor? photoCompressor = null,
        ISmartCompressor? videoCompressor = null)
    {
        _registry = registry;
        _photoCompressor = photoCompressor;
        _videoCompressor = videoCompressor;
        _uiContext = SynchronizationContext.Current;
        MaxConcurrentJobs = maxConcurrentJobs;
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentJobs, maxConcurrentJobs);
        _channel = Channel.CreateBounded<QueuedJob>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        // Start background consumer tasks
        for (int i = 0; i < maxConcurrentJobs; i++)
            _workers.Add(Task.Run(ConsumeLoopAsync));
    }

    public async Task EnqueueConversionAsync(ConversionJob job)
    {
        var cts = new CancellationTokenSource();
        lock (_cancellations) _cancellations[job.Id] = cts;
        await _channel.Writer.WriteAsync(new QueuedJob(job.Id, job, null, cts.Token));
        RaiseEnqueued(new JobEnqueuedEventArgs(job.Id, Path.GetFileName(job.InputPath), $"Convert to {job.TargetExtension}"));
    }

    public async Task EnqueueCompressionAsync(CompressJob job)
    {
        var cts = new CancellationTokenSource();
        lock (_cancellations) _cancellations[job.Id] = cts;
        await _channel.Writer.WriteAsync(new QueuedJob(job.Id, null, job, cts.Token));
        RaiseEnqueued(new JobEnqueuedEventArgs(job.Id, Path.GetFileName(job.InputPath), "Compressing " + Path.GetExtension(job.InputPath)));
    }

    public void CancelAll()
    {
        lock (_cancellations)
        {
            foreach (var cts in _cancellations.Values)
            {
                try { cts.Cancel(); } catch { }
            }
            _cancellations.Clear();
        }
    }

    public void CancelJob(Guid jobId)
    {
        lock (_cancellations)
        {
            if (_cancellations.TryGetValue(jobId, out var cts))
            {
                try
                {
                    cts.Cancel();
                }
                catch (AggregateException ex)
                {
                    // Ignore exceptions from CancellationToken callbacks (e.g. Process.Kill on exited process)
                    System.Diagnostics.Debug.WriteLine($"Cancellation callback threw: {ex.InnerException?.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cancelling job: {ex.Message}");
                }
                finally
                {
                    _cancellations.Remove(jobId);
                }
            }
        }
    }

    public void PauseQueue() => _paused = true;
    public void ResumeQueue() => _paused = false;

    private async Task ConsumeLoopAsync()
    {
        await foreach (var queued in _channel.Reader.ReadAllAsync())
        {
            // Honour pause state — spin-wait until unpaused
            while (_paused)
                await Task.Delay(250);

            if (queued.CancellationToken.IsCancellationRequested)
            {
                var input = queued.ConversionJob?.InputPath ?? queued.CompressionJob?.InputPath ?? string.Empty;
                var output = queued.ConversionJob?.OutputPath ?? queued.CompressionJob?.OutputPath ?? string.Empty;
                var result = new JobResult(queued.Id, false, input, output, 0, 0, TimeSpan.Zero, "Cancelled", true);
                RaiseCompleted(result);
                lock (_cancellations) _cancellations.Remove(queued.Id);
                continue;
            }

            await _concurrencySemaphore.WaitAsync();
            try
            {
                await ProcessJobAsync(queued);
            }
            finally
            {
                _concurrencySemaphore.Release();
                lock (_cancellations) _cancellations.Remove(queued.Id);
            }
        }
    }

    private async Task ProcessJobAsync(QueuedJob queued)
    {
        var progress = new Progress<JobProgress>(p => RaiseProgress(p));

        JobResult? result = null;
        try
        {
            if (queued.ConversionJob is { } convJob)
            {
                var engine = _registry.Resolve(
                    Path.GetExtension(convJob.InputPath).ToLowerInvariant(),
                    convJob.TargetExtension.ToLowerInvariant());

                if (engine is null)
                    throw new InvalidOperationException(
                        $"No engine found for {Path.GetExtension(convJob.InputPath)} ? {convJob.TargetExtension}");

                result = await engine.ConvertAsync(convJob, progress, queued.CancellationToken);
            }
            else if (queued.CompressionJob is { } compJob)
            {
                var ext = Path.GetExtension(compJob.InputPath).ToLowerInvariant();
                var category = _registry.GetCategory(ext);

                ISmartCompressor compressor = category == FileCategory.Image
                    ? _photoCompressor ?? throw new InvalidOperationException("Photo compressor not registered")
                    : _videoCompressor ?? throw new InvalidOperationException("Video compressor not registered");

                var compressResult = await compressor.CompressAsync(compJob, progress, queued.CancellationToken);
                var inputInfo = new System.IO.FileInfo(compJob.InputPath);
                var outputInfo = System.IO.File.Exists(compJob.OutputPath) ? new System.IO.FileInfo(compJob.OutputPath) : null;
                result = new JobResult(
                    compressResult.JobId,
                    compressResult.Success,
                    compJob.InputPath,
                    compJob.OutputPath,
                    inputInfo.Exists ? inputInfo.Length : 0,
                    outputInfo?.Length ?? 0,
                    compressResult.Duration,
                    compressResult.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            var input = queued.ConversionJob?.InputPath ?? queued.CompressionJob?.InputPath ?? string.Empty;
            var output = queued.ConversionJob?.OutputPath ?? queued.CompressionJob?.OutputPath ?? string.Empty;
            result = new JobResult(queued.Id, false, input, output, 0, 0, TimeSpan.Zero, "Cancelled by user", true);
        }
        catch (Exception ex)
        {
            var input = queued.ConversionJob?.InputPath ?? queued.CompressionJob?.InputPath ?? string.Empty;
            var output = queued.ConversionJob?.OutputPath ?? queued.CompressionJob?.OutputPath ?? string.Empty;
            if (queued.CancellationToken.IsCancellationRequested)
            {
                result = new JobResult(queued.Id, false, input, output, 0, 0, TimeSpan.Zero, "Cancelled by user", true);
            }
            else
            {
                result = new JobResult(queued.Id, false, input, output, 0, 0, TimeSpan.Zero, ex.Message);
            }
        }

        RaiseCompleted(result ?? new JobResult(queued.Id, false,
            queued.ConversionJob?.InputPath ?? queued.CompressionJob?.InputPath ?? string.Empty,
            queued.ConversionJob?.OutputPath ?? queued.CompressionJob?.OutputPath ?? string.Empty,
            0, 0, TimeSpan.Zero, "Unknown job type"));
    }

    private void RaiseProgress(JobProgress progress)
    {
        var args = new JobProgressEventArgs(progress.JobId, progress);
        if (_uiContext != null)
            _uiContext.Post(_ => JobProgressChanged?.Invoke(this, args), null);
        else
            JobProgressChanged?.Invoke(this, args);
    }

    private void RaiseCompleted(JobResult result)
    {
        var args = new JobCompletedEventArgs(result.JobId, result);
        if (_uiContext != null)
            _uiContext.Post(_ => JobCompleted?.Invoke(this, args), null);
        else
            JobCompleted?.Invoke(this, args);
    }

    private void RaiseEnqueued(JobEnqueuedEventArgs args)
    {
        if (_uiContext != null)
            _uiContext.Post(_ => JobEnqueued?.Invoke(this, args), null);
        else
            JobEnqueued?.Invoke(this, args);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Writer.Complete();
        _concurrencySemaphore.Dispose();
    }

    private record QueuedJob(Guid Id, ConversionJob? ConversionJob, CompressJob? CompressionJob, CancellationToken CancellationToken);
}
