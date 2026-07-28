using FormatFusion.Core.Models;
using System.Text.Json;

namespace FormatFusion.Core.Services;

/// <summary>
/// Persists recent job history to a local JSON file.
/// Max 50 entries; oldest entries are pruned automatically.
/// </summary>
public sealed class RecentJobsService
{
    private const int MaxEntries = 50;
    private static readonly string StorePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FormatFusion", "recent_jobs.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task AddAsync(RecentJobRecord record)
    {
        var list = await LoadAsync();
        list.Insert(0, record);
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        await SaveAsync(list);
    }

    public async Task<List<RecentJobRecord>> LoadAsync()
    {
        try
        {
            if (!File.Exists(StorePath)) return new List<RecentJobRecord>();
            var json = await File.ReadAllTextAsync(StorePath);
            return JsonSerializer.Deserialize<List<RecentJobRecord>>(json, JsonOptions)
                   ?? new List<RecentJobRecord>();
        }
        catch { return new List<RecentJobRecord>(); }
    }

    public async Task ClearAsync()
    {
        if (File.Exists(StorePath))
            await Task.Run(() => File.Delete(StorePath));
    }

    private async Task SaveAsync(List<RecentJobRecord> records)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        var json = JsonSerializer.Serialize(records, JsonOptions);
        await File.WriteAllTextAsync(StorePath, json);
    }
}
