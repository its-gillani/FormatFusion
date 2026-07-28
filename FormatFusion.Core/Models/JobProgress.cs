namespace FormatFusion.Core.Models;

/// <summary>
/// Progress snapshot for a running job. Marshalled to UI thread via IProgress&lt;T&gt;.
/// </summary>
public record JobProgress(
    Guid JobId,
    double PercentComplete,       // 0.0 – 100.0
    string CurrentPhase,          // e.g. "Encoding", "Pass 1/2", "Estimating"
    TimeSpan? EstimatedRemaining,
    long? BytesProcessed = null);
