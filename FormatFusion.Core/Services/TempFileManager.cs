namespace FormatFusion.Core.Services;

/// <summary>
/// Tracks all temp files created during jobs and cleans them up.
/// On startup, orphaned files from previous crashed sessions are also removed.
/// </summary>
public sealed class TempFileManager : IDisposable
{
    private static readonly string TempDir =
        Path.Combine(Path.GetTempPath(), "FormatFusion");

    private readonly HashSet<string> _tracked = new();
    private readonly object _lock = new();

    public TempFileManager()
    {
        Directory.CreateDirectory(TempDir);
        CleanOrphans();
    }

    /// <summary>Create a new temp file path with the given extension. File is not created on disk yet.</summary>
    public string GetTempPath(string extension)
    {
        if (!extension.StartsWith('.')) extension = "." + extension;
        var path = Path.Combine(TempDir, $"{Guid.NewGuid():N}{extension}");
        lock (_lock) _tracked.Add(path);
        return path;
    }

    /// <summary>Delete a specific temp file immediately.</summary>
    public void Release(string path)
    {
        lock (_lock) _tracked.Remove(path);
        TryDelete(path);
    }

    /// <summary>Delete all tracked temp files.</summary>
    public void ReleaseAll()
    {
        string[] paths;
        lock (_lock)
        {
            paths = _tracked.ToArray();
            _tracked.Clear();
        }
        foreach (var p in paths) TryDelete(p);
    }

    private void CleanOrphans()
    {
        if (!Directory.Exists(TempDir)) return;
        foreach (var file in Directory.GetFiles(TempDir))
            TryDelete(file);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* Swallow — file may be locked or already gone */ }
    }

    public void Dispose() => ReleaseAll();
}
