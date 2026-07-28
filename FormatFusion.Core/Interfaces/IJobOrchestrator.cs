using FormatFusion.Core.Models;

namespace FormatFusion.Core.Interfaces;

/// <summary>
/// Manages the job queue: enqueues, cancels, pauses, resumes jobs.
/// Runs conversion/compression on background threads via Channel&lt;T&gt;.
/// </summary>
public interface IJobOrchestrator
{
    /// <summary>Add a conversion job to the queue.</summary>
    Task EnqueueConversionAsync(ConversionJob job);

    /// <summary>Add a compression job to the queue.</summary>
    Task EnqueueCompressionAsync(CompressJob job);

    /// <summary>Cancel a specific job by ID. In-progress jobs are signalled via CancellationToken.</summary>
    void CancelJob(Guid jobId);
    void CancelAll();

    /// <summary>Pause queuing: current in-progress jobs finish; no new jobs start.</summary>
    void PauseQueue();

    /// <summary>Resume a paused queue.</summary>
    void ResumeQueue();

    /// <summary>True when the queue is paused.</summary>
    bool IsPaused { get; }

    /// <summary>Number of jobs currently running (not queued, actively processing).</summary>
    int ActiveJobCount { get; }

    /// <summary>Raised on the UI thread whenever a job's progress changes.</summary>
    event EventHandler<JobProgressEventArgs> JobProgressChanged;

    /// <summary>Raised on the UI thread when a job completes (success or failure).</summary>
    event EventHandler<JobCompletedEventArgs> JobCompleted;

    /// <summary>Raised on the UI thread when a new job is enqueued.</summary>
    event EventHandler<JobEnqueuedEventArgs> JobEnqueued;
}

public record JobProgressEventArgs(Guid JobId, JobProgress Progress);
public record JobCompletedEventArgs(Guid JobId, JobResult Result);
public record JobEnqueuedEventArgs(Guid JobId, string FileName, string OperationLabel);
