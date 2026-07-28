using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatFusion.Core;
using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using FormatFusion.UI.Helpers;
using System.Diagnostics;

namespace FormatFusion.UI.ViewModels;

public partial class JobViewModel : ObservableObject
{
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _currentPhase = "Queued";
    [ObservableProperty] private string _etaLabel = string.Empty;
    [ObservableProperty] private string _durationLabel = string.Empty;
    [ObservableProperty] private JobStatus _status = JobStatus.Queued;
    [ObservableProperty] private string _sizeLabel = string.Empty;
    [ObservableProperty] private string _fileTypeIcon = "";

    public Guid JobId { get; }
    public string FileName { get; }
    public string OperationLabel { get; }
    public string OriginalFormat { get; }
    public string TargetFormat { get; }

    public string StatusIcon => Status switch
    {
        JobStatus.Queued => "-",
        JobStatus.Running => "Y3",
        JobStatus.Completed => "o.",
        JobStatus.Failed => "?O",
        JobStatus.Cancelled => "o ",
        _ => "?"
    };

    public bool IsRunning => Status == JobStatus.Running;

    private readonly IJobOrchestrator _orchestrator;
    private readonly IFormatRegistry _registry;
    private readonly Action? _onCancelLocal;

    public JobViewModel(Guid jobId, string fileName, string operation, IJobOrchestrator orchestrator, IFormatRegistry registry, Action? onCancelLocal = null)
    {
        JobId = jobId;
        FileName = fileName;
        _orchestrator = orchestrator;
        _registry = registry;
        _onCancelLocal = onCancelLocal;
        CancelCommand = new RelayCommand(Cancel);
        OpenFileCommand = new RelayCommand(OpenFile);
        SetFileTypeIcon();
        
        // Extract formats for arrows
        OriginalFormat = System.IO.Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        if (operation.Contains(" to "))
        {
            TargetFormat = operation.Split(" to ")[1].TrimStart('.').ToUpperInvariant();
        }
        else if (operation.StartsWith("Compressing "))
        {
            TargetFormat = OriginalFormat; // same format
        }
        else
        {
            TargetFormat = "?";
        }
        OperationLabel = string.IsNullOrEmpty(TargetFormat) ? OriginalFormat : $"{OriginalFormat} \u2192 {TargetFormat}";
    }

    private void SetFileTypeIcon()
    {
        var ext = System.IO.Path.GetExtension(FileName).ToLowerInvariant();
        var cat = _registry.GetCategory(ext);
        FileTypeIcon = IconHelper.GetIcon(cat);
    }

    public IRelayCommand CancelCommand { get; }
    public IRelayCommand OpenFileCommand { get; }
    public string OutputPath { get; private set; } = string.Empty;
    public bool CanOpenFile => Status == JobStatus.Completed && System.IO.File.Exists(OutputPath);

    private void Cancel()
    {
        _orchestrator.CancelJob(JobId);
        Status = JobStatus.Cancelled;
        CurrentPhase = "Cancelled";
        DurationLabel = "Time Taken: N/A";
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StatusIcon));
        _onCancelLocal?.Invoke();
    }

    private void OpenFile()
    {
        if (CanOpenFile)
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{OutputPath}\"");
        }
    }

    public void ApplyProgress(JobProgress progress)
    {
        if (Status == JobStatus.Completed || Status == JobStatus.Failed || Status == JobStatus.Cancelled)
            return;

        Status = JobStatus.Running;
        ProgressPercent = progress.PercentComplete;
        CurrentPhase = progress.CurrentPhase;
        EtaLabel = progress.EstimatedRemaining.HasValue
            ? $"ETA: {FormatEta(progress.EstimatedRemaining.Value, progress.PercentComplete)}"
            : string.Empty;
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StatusIcon));
    }

    public void ApplyResult(JobResult result)
    {
        if (result.Cancelled)
        {
            Status = JobStatus.Cancelled;
            CurrentPhase = "Cancelled";
            ProgressPercent = 0;
            EtaLabel = string.Empty;
            DurationLabel = "Time Taken: N/A";
        }
        else
        {
            Status = result.Success ? JobStatus.Completed : JobStatus.Failed;
            ProgressPercent = result.Success ? 100 : ProgressPercent;
            CurrentPhase = result.Success ? "Done" : result.ErrorMessage ?? "Failed";
            OutputPath = result.OutputPath;
            EtaLabel = string.Empty;
            
            var durationSecs = (int)result.Duration.TotalSeconds;
            if (result.Success)
            {
                DurationLabel = string.IsNullOrEmpty(result.BackendUsed) || result.BackendUsed == "CPU"
                    ? $"Time Taken: {durationSecs}s"
                    : $"Time Taken: {durationSecs}s \u00b7 {result.BackendUsed}";
            }
            else
            {
                DurationLabel = "Time Taken: N/A";
            }
            
            SizeLabel = result.Success
                ? $"{result.InputSizeFormatted} \u2192 {result.OutputSizeFormatted} ({result.SavingsPercent:+0.#;-0.#;+0}%)"
                : string.Empty;
        }
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(CanOpenFile));
    }

    private static string FormatEta(TimeSpan eta, double percent) 
    {
        if (percent < 1.0) return "Calculating...";
        if (percent > 98.0 || eta.TotalSeconds < 2) return "Almost done";
        return eta.TotalSeconds switch
        {
            < 60 => $"{(int)eta.TotalSeconds}s",
            < 3600 => $"{(int)eta.TotalMinutes}m {eta.Seconds}s",
            _ => $"{(int)eta.TotalHours}h {eta.Minutes}m"
        };
    }
}

public partial class QueueViewModel : ObservableObject
{
    private readonly IJobOrchestrator _orchestrator;
    private readonly FormatFusion.Core.Services.RecentJobsService _recentJobsService;
    private readonly IFormatRegistry _registry;
    private readonly IUserPromptService _promptService;
    private readonly Dictionary<Guid, JobViewModel> _jobMap = new();

    [ObservableProperty] private bool _isRunning = false;
    [ObservableProperty] private string _statusSummary = "Queue";
    [ObservableProperty] private string _cpuUsageLabel = "CPU: --";
    [ObservableProperty] private string _activeJobsLabel = "Active: 0";

    public ObservableCollection<JobViewModel> Jobs { get; } = new();

    public QueueViewModel(IJobOrchestrator orchestrator, FormatFusion.Core.Services.RecentJobsService recentJobsService, IFormatRegistry registry, IUserPromptService promptService)
    {
        _orchestrator = orchestrator;
        _recentJobsService = recentJobsService;
        _registry = registry;
        _promptService = promptService;
        _orchestrator.JobEnqueued += OnJobEnqueued;
        _orchestrator.JobProgressChanged += OnJobProgress;
        _orchestrator.JobCompleted += OnJobCompleted;
        StartResourceMonitor();
    }

    private void OnJobEnqueued(object? sender, JobEnqueuedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => AddJob(e.JobId, e.FileName, e.OperationLabel));
    }

    public void AddJob(Guid id, string fileName, string operation)
    {
        var vm = new JobViewModel(id, fileName, operation, _orchestrator, _registry, UpdateSummary);
        _jobMap[id] = vm;
        Jobs.Insert(0, vm);
        UpdateSummary();
        IsRunning = true;
    }

    private void OnJobProgress(object? sender, JobProgressEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
        {
            if (_jobMap.TryGetValue(e.JobId, out var vm))
                vm.ApplyProgress(e.Progress);
        });
    }

    private void OnJobCompleted(object? sender, JobCompletedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
        {
            if (_jobMap.TryGetValue(e.JobId, out var vm))
            {
                vm.ApplyResult(e.Result);
                UpdateSummary();

                if (e.Result.Success && !e.Result.Cancelled)
                {
                    var record = new RecentJobRecord(
                        e.JobId,
                        vm.FileName,
                        e.Result.InputPath,
                        e.Result.OutputPath,
                        vm.OperationLabel,
                        e.Result.Success,
                        e.Result.InputSizeBytes,
                        e.Result.OutputSizeBytes,
                        DateTime.UtcNow);
                    _ = _recentJobsService.AddAsync(record);
                }
            }
        });
    }

    private void UpdateSummary()
    {
        var total = Jobs.Count;
        var done = Jobs.Count(j => j.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled);
        var running = Jobs.Count(j => j.Status == JobStatus.Running);
        
        bool justFinished = IsRunning && running == 0 && done == total && total > 0;
        
        StatusSummary = running > 0
            ? $"Processing ({done} of {total})"
            : done == total && total > 0
                ? $"All Done - {total} files processed"
                : $"Queue ({total} files)";
        IsRunning = running > 0;
        ActiveJobsLabel = $"Active: {running}";
        
        if (justFinished)
        {
            var successes = Jobs.Count(j => j.Status == JobStatus.Completed);
            var failures = Jobs.Count(j => j.Status == JobStatus.Failed);
            var cancelled = Jobs.Count(j => j.Status == JobStatus.Cancelled);
            
            string content = $"{successes} succeeded";
            if (failures > 0) content += $", {failures} failed";
            if (cancelled > 0) content += $", {cancelled} cancelled";
            
            FormatFusion.UI.Services.NotificationService.ShowToast("Queue Completed", content + ".");
        }
    }

    private void StartResourceMonitor()
    {
        _ = Task.Run(async () =>
        {
            var envCores = Environment.ProcessorCount;
            var procMap = new Dictionary<int, (TimeSpan totalCpu, DateTime lastTime)>();

            while (true)
            {
                await Task.Delay(2000);
                
                string cpuText = "CPU: 0%";
                string gpuText = "GPU: 0%";
                bool hasActive = false;

                try
                {
                    var procs = Process.GetProcessesByName("ffmpeg").Concat(Process.GetProcessesByName("ffprobe")).ToList();
                    hasActive = procs.Count > 0;

                    if (hasActive)
                    {
                        double totalCpuUsage = 0;
                        var now = DateTime.UtcNow;

                        foreach (var p in procs)
                        {
                            try
                            {
                                var currentCpu = p.TotalProcessorTime;
                                if (procMap.TryGetValue(p.Id, out var last))
                                {
                                    var timeDelta = (now - last.lastTime).TotalMilliseconds;
                                    if (timeDelta > 0)
                                    {
                                        var cpuDelta = (currentCpu - last.totalCpu).TotalMilliseconds;
                                        var usage = (cpuDelta / timeDelta) / envCores * 100.0;
                                        totalCpuUsage += usage;
                                    }
                                }
                                procMap[p.Id] = (currentCpu, now);
                            }
                            catch { }
                        }
                        
                        // Cleanup dead procs
                        var activeIds = procs.Select(p => p.Id).ToHashSet();
                        foreach (var id in procMap.Keys.ToList())
                            if (!activeIds.Contains(id)) procMap.Remove(id);

                        cpuText = $"CPU: {Math.Min(100, totalCpuUsage):0}%";

                        // GPU tracking via PerformanceCounter
#pragma warning disable CA1416
                        try
                        {
                            var category = new PerformanceCounterCategory("GPU Engine");
                            var instances = category.GetInstanceNames();
                            float totalGpu = 0;
                            foreach (var instance in instances)
                            {
                                // Format: pid_<ID>_luid_<ID>_phys_<ID>_eng_<ID>_engtype_3D
                                if (instance.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
                                {
                                    var parts = instance.Split('_');
                                    if (parts.Length > 1 && int.TryParse(parts[1], out int pid) && activeIds.Contains(pid))
                                    {
                                        using var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                                        counter.NextValue();
                                        System.Threading.Thread.Sleep(50); // Sample
                                        totalGpu += counter.NextValue();
                                    }
                                }
                            }
                            gpuText = $"GPU: {totalGpu:0}%";
                        }
                        catch
                        {
                            gpuText = "GPU: --"; // Fallback if no permissions
                        }
#pragma warning restore CA1416
                    }
                    else
                    {
                        procMap.Clear();
                    }
                }
                catch { }

                var label = $"Active: {_orchestrator.ActiveJobCount}";
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    ActiveJobsLabel = label;
                    CpuUsageLabel = hasActive ? $"{cpuText} | {gpuText}" : "CPU: -- | GPU: --";
                });
            }
        });
    }

    [RelayCommand]
    private void PauseAll()
    {
        if (_orchestrator.IsPaused) _orchestrator.ResumeQueue();
        else _orchestrator.PauseQueue();
    }

    [RelayCommand]
    private void CancelAll()
    {
        var activeJobs = Jobs.Where(j => j.Status == JobStatus.Running || j.Status == JobStatus.Queued).ToList();
        if (activeJobs.Count == 0) return;

        if (_promptService.PromptUser($"Cancel all {activeJobs.Count} in-progress jobs? This cannot be undone.", "Cancel All Jobs"))
        {
            foreach (var job in activeJobs)
            {
                _orchestrator.CancelJob(job.JobId);
                job.Status = JobStatus.Cancelled;
                job.CurrentPhase = "Cancelled";
                job.DurationLabel = "Time Taken: N/A";
            }
            UpdateSummary();
        }
    }
}
