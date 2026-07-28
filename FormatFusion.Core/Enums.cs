namespace FormatFusion.Core;

/// <summary>File category enum used throughout the app for routing to engines.</summary>
public enum FileCategory
{
    Unknown,
    Image,
    Audio,
    Video,
    Document,
    Archive
}

/// <summary>Status of a job in the queue.</summary>
public enum JobStatus
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
