namespace FormatFusion.Core.Models;

/// <summary>
/// Persistent record of a completed job, stored in recent history.
/// Serialized to JSON by RecentJobsService.
/// </summary>
public record RecentJobRecord(
    Guid Id,
    string FileName,
    string InputPath,
    string OutputPath,
    string Operation,           // e.g. "JPG → WEBP" or "Compress to 20 MB"
    bool Success,
    long InputSizeBytes,
    long OutputSizeBytes,
    DateTime CompletedAt)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - CompletedAt;
            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }
}
