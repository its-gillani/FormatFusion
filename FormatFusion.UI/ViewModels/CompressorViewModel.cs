using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatFusion.Core;
using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using FormatFusion.Compression;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FormatFusion.UI.ViewModels;

public partial class CompressorViewModel : ObservableObject
{
    private readonly PhotoCompressor _photoCompressor;
    private readonly VideoCompressor _videoCompressor;
    private readonly IJobOrchestrator _orchestrator;
    private readonly IFormatRegistry _registry;
    private readonly FormatFusion.Core.Services.AppSettings _appSettings;

    public FormatFusion.Core.Services.AppSettings AppSettings => _appSettings;

    private CancellationTokenSource? _estimateCts;

    [ObservableProperty] private string _categoryIcon = "";
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileName = "No file selected";
    [ObservableProperty] private string _fileInfo = string.Empty;
    [ObservableProperty] private string _targetSizeMB = "20";
    [ObservableProperty] private string _outputFolder;
    [ObservableProperty] private string _selectedOutputFormat = ".mp4";
    [ObservableProperty] private bool _hasEstimate = false;
    [ObservableProperty] private string _estimateSizeFormatted = string.Empty;
    [ObservableProperty] private string _originalSizeFormatted = string.Empty;
    [ObservableProperty] private string _targetSizeFormatted = string.Empty;
    [ObservableProperty] private double _estimateBarPercent = 0;
    [ObservableProperty] private double _targetBarPercent = 0;
    [ObservableProperty] private string _estimateBadgeText = "Achievable";
    [ObservableProperty] private Brush _estimateBadgeBackground = new SolidColorBrush(Color.FromRgb(0x2D, 0xD4, 0xBF));
    [ObservableProperty] private string _estimateDetails = string.Empty;
    [ObservableProperty] private bool _showQualityWarning = false;
    [ObservableProperty] private string _qualityWarningText = string.Empty;
    [ObservableProperty] private bool _canCompress = false;
    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<string> _outputFormats = new();
    public System.Collections.ObjectModel.ObservableCollection<BackendOptionViewModel> BackendOptions { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<CodecOptionViewModel> GlobalCodecOptions { get; } = new();

    private double _originalSizeMB;
    private FileCategory _category;

    public CompressorViewModel(
        PhotoCompressor photoCompressor,
        VideoCompressor videoCompressor,
        IJobOrchestrator orchestrator,
        IFormatRegistry registry,
        FormatFusion.Core.Services.AppSettings appSettings)
    {
        _photoCompressor = photoCompressor;
        _videoCompressor = videoCompressor;
        _orchestrator = orchestrator;
        _registry = registry;
        _appSettings = appSettings;
        _outputFolder = _appSettings.DefaultOutputFolder;
        UpdateBackendOptions();
        InitializeGlobalCodecOptions();
        _appSettings.SettingsChanged += () =>
        {
            UpdateBackendOptions();
            UpdateGlobalCodecOptions();
        };
    }

    private void UpdateBackendOptions()
    {
        var caps = _appSettings.HardwareCaps;
        var options = new System.Collections.Generic.List<BackendOptionViewModel>
        {
            new("Auto (Recommended)"),
            new("CPU"),
            new("NVIDIA GPU", caps?.NvidiaUsable == true),
            new("AMD GPU", caps?.AmdUsable == true),
            new("Intel GPU", caps?.IntelUsable == true)
        };

        BackendOptions.Clear();
        foreach (var opt in options)
        {
            BackendOptions.Add(opt);
        }
    }

    private void InitializeGlobalCodecOptions()
    {
        var codecs = new[] { "Default", "H.264", "H.265", "VP9", "AV1" };
        foreach (var c in codecs)
        {
            GlobalCodecOptions.Add(new CodecOptionViewModel(c));
        }
        UpdateGlobalCodecOptions();
    }

    [ObservableProperty] private string _conflictMessage = "";
    [ObservableProperty] private Brush? _conflictMessageBrush;
    [ObservableProperty] private bool _showConflictMessage = false;

    private void UpdateGlobalCodecOptions()
    {
        var backend = _appSettings.HardwareBackend;
        var caps = _appSettings.HardwareCaps;
        var targetExt = string.IsNullOrEmpty(SelectedOutputFormat) ? Path.GetExtension(FilePath ?? "").ToLowerInvariant() : "." + SelectedOutputFormat.TrimStart('.');

        foreach (var opt in GlobalCodecOptions)
        {
            FormatFusion.UI.Helpers.CompatibilityHelper.EvaluateCodecOption(opt, targetExt, backend, caps);
        }

        UpdateWarnings();
    }

    private void UpdateWarnings()
    {
        var backend = _appSettings.HardwareBackend;
        var caps = _appSettings.HardwareCaps;
        var targetExt = string.IsNullOrEmpty(SelectedOutputFormat) ? Path.GetExtension(FilePath ?? "").ToLowerInvariant() : "." + SelectedOutputFormat.TrimStart('.');
        var codec = _category == FileCategory.Video ? _appSettings.VideoCodec : null;

        if (codec != null && !FormatFusion.UI.Helpers.CompatibilityHelper.IsCodecSupportedByContainer(codec, targetExt))
        {
            ConflictMessage = $"⛔ {codec} is not supported in {targetExt} files — choose a different codec or output format.";
            ConflictMessageBrush = FormatFusion.UI.Helpers.CompatibilityHelper.GetStructuralConflictBrush();
            ShowConflictMessage = true;
            return;
        }

        var activeBackend = backend;
        if (activeBackend == "Auto (Recommended)")
        {
            if (caps?.NvidiaUsable == true) activeBackend = "NVIDIA GPU";
            else if (caps?.AmdUsable == true) activeBackend = "AMD GPU";
            else if (caps?.IntelUsable == true) activeBackend = "Intel GPU";
            else activeBackend = "CPU";
        }

        if (backend != "Auto (Recommended)")
        {
            var backendOpt = BackendOptions.FirstOrDefault(b => b.BackendName == backend);
            if (backendOpt != null && !backendOpt.IsDetected)
            {
                ConflictMessage = $"⚠ {backend} was not detected on this system — select Auto or a different available backend.";
                ConflictMessageBrush = FormatFusion.UI.Helpers.CompatibilityHelper.GetHardwareConflictBrush();
                ShowConflictMessage = true;
                return;
            }

            if (codec != null && !FormatFusion.UI.Helpers.CompatibilityHelper.IsCodecSupportedByBackend(codec, activeBackend, caps))
            {
                ConflictMessage = $"⚠ {codec} is not supported by {backend} — switch to CPU, a different codec, or a different hardware option.";
                ConflictMessageBrush = FormatFusion.UI.Helpers.CompatibilityHelper.GetHardwareConflictBrush();
                ShowConflictMessage = true;
                return;
            }
        }

        ShowConflictMessage = false;
    }

    partial void OnSelectedOutputFormatChanged(string value)
    {
        UpdateGlobalCodecOptions();
    }

    public void LoadFile(string path)
    {
        FilePath = path;
        FileName = Path.GetFileName(path);
        var info = new FileInfo(path);
        _originalSizeMB = info.Length / 1_048_576.0;
        OriginalSizeFormatted = $"{_originalSizeMB:F1} MB";

        var ext = Path.GetExtension(path).ToLowerInvariant();
        _category = _registry.GetCategory(ext);
        CategoryIcon = FormatFusion.UI.Helpers.IconHelper.GetIcon(_category);

        FileInfo = _category switch
        {
            FileCategory.Image => $"{_originalSizeMB:F1} MB · Image",
            FileCategory.Video => $"{_originalSizeMB:F1} MB · Video",
            _ => $"{_originalSizeMB:F1} MB"
        };

        var formats = _registry.GetOutputFormats(ext).ToList();
        if (!formats.Contains(ext)) formats.Insert(0, ext);
        OutputFormats.Clear();
        foreach (var format in formats) OutputFormats.Add(format);

        SelectedOutputFormat = OutputFormats.FirstOrDefault() ?? ext;

        HasEstimate = false;
        CanCompress = false;
        _ = TriggerEstimateAsync();
    }

    partial void OnTargetSizeMBChanged(string value)
        => _ = TriggerEstimateAsync();

    private async Task TriggerEstimateAsync()
    {
        _estimateCts?.Cancel();
        _estimateCts = new CancellationTokenSource();
        var ct = _estimateCts.Token;

        if (!double.TryParse(TargetSizeMB, out var targetMB) || targetMB <= 0 || string.IsNullOrEmpty(FilePath))
        {
            HasEstimate = false;
            CanCompress = false;
            return;
        }

        await Task.Delay(500, ct).ConfigureAwait(false); // Debounce 500ms
        if (ct.IsCancellationRequested) return;

        try
        {
            ISmartCompressor compressor = _category == FileCategory.Image
                ? _photoCompressor
                : _videoCompressor;

            var estimate = await compressor.EstimateOutputSizeAsync(FilePath, targetMB, ct);

            if (ct.IsCancellationRequested) return;

            // Update UI (we're on background thread — use dispatcher)
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                HasEstimate = true;
                EstimateSizeFormatted = $"~{estimate.EstimatedSizeMB:F1} MB";
                TargetSizeFormatted = $"{targetMB:F0} MB";
                TargetBarPercent = Math.Min(100, targetMB / _originalSizeMB * 100);
                EstimateBarPercent = Math.Min(100, estimate.EstimatedSizeMB / _originalSizeMB * 100);

                switch (estimate.Warning)
                {
                    case EstimateQualityWarning.None:
                        EstimateBadgeText = "Achievable";
                        EstimateBadgeBackground = new SolidColorBrush(Color.FromRgb(0x2D, 0xD4, 0xBF));
                        ShowQualityWarning = false;
                        CanCompress = true;
                        break;
                    case EstimateQualityWarning.QualityWillBeReduced:
                        EstimateBadgeText = "Quality Impact";
                        EstimateBadgeBackground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                        QualityWarningText = $"At this target, quality will be noticeably reduced. Consider {targetMB * 1.5:F0} MB for better results.";
                        ShowQualityWarning = true;
                        CanCompress = true;
                        break;
                    case EstimateQualityWarning.DownscaleRequired:
                        EstimateBadgeText = "Downscale Needed";
                        EstimateBadgeBackground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                        QualityWarningText = $"Resolution reduction required to reach {targetMB:F0} MB.";
                        ShowQualityWarning = true;
                        CanCompress = true;
                        break;
                    case EstimateQualityWarning.ImpossibleTooSmall:
                        EstimateBadgeText = "Too Small";
                        EstimateBadgeBackground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
                        QualityWarningText = "Target is too small to achieve even at minimum quality.";
                        ShowQualityWarning = true;
                        CanCompress = false;
                        break;
                }

                EstimateDetails = estimate.DownscaleRequired
                    ? $"Resolution: {estimate.ResolutionWillBe}"
                    : _category == FileCategory.Video && estimate.EstimatedBitrateKbps > 0
                        ? $"Est. video bitrate: {estimate.EstimatedBitrateKbps:F0} kbps · Resolution: {estimate.ResolutionWillBe}"
                        : $"Resolution: {estimate.ResolutionWillBe}";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                HasEstimate = false;
                EstimateDetails = $"Estimate failed: {ex.Message}";
            });
        }
    }

    [RelayCommand]
    private async Task Compress()
    {
        if (!CanCompress || !double.TryParse(TargetSizeMB, out var targetMB)) return;

        var selectedBackend = AppSettings.HardwareBackend;
        var backendOpt = BackendOptions.FirstOrDefault(b => b.BackendName == selectedBackend);

        var targetExt = string.IsNullOrEmpty(SelectedOutputFormat) ? Path.GetExtension(FilePath ?? "").ToLowerInvariant() : "." + SelectedOutputFormat.TrimStart('.');
        var resolvedCodec = _category == FileCategory.Video ? AppSettings.VideoCodec : null;

        if (resolvedCodec != null && !FormatFusion.UI.Helpers.CompatibilityHelper.IsCodecSupportedByContainer(resolvedCodec, targetExt))
        {
            string choice = "Cancel";
            Application.Current.Dispatcher.Invoke(() => 
            {
                var win = new FormatFusion.UI.Views.ThemedDialogWindow(
                    $"⛔ {resolvedCodec} is not supported in {targetExt} files.\n\nThis format and codec cannot be combined.",
                    "Unsupported Combination", hideCancel: false, proceedText: "Choose a different codec", extraText: "Choose a different format");
                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded) win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
                choice = win.CustomResult;
            });
            if (choice == "Cancel") return;
            return;
        }

        var forceCpuForJobs = false;

        if (selectedBackend != "Auto (Recommended)")
        {
            if (backendOpt != null && !backendOpt.IsDetected)
            {
                string choice = "Cancel";
                Application.Current.Dispatcher.Invoke(() => 
                {
                    var win = new FormatFusion.UI.Views.ThemedDialogWindow(
                        $"⚠ The selected hardware acceleration '{selectedBackend}' was not detected on this system.",
                        "Invalid Hardware Backend", hideCancel: false, proceedText: "Use CPU instead", extraText: "Choose a different backend");
                    if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded) win.Owner = Application.Current.MainWindow;
                    win.ShowDialog();
                    choice = win.CustomResult;
                });
                if (choice == "Cancel" || choice == "Extra") return;
                if (choice == "Proceed") forceCpuForJobs = true;
            }
            else
            {
                if (resolvedCodec != null && !FormatFusion.UI.Helpers.CompatibilityHelper.IsCodecSupportedByBackend(resolvedCodec, selectedBackend, AppSettings.HardwareCaps))
                {
                    string choice = "Cancel";
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        var win = new FormatFusion.UI.Views.ThemedDialogWindow(
                            $"⚠ {resolvedCodec} is not supported by {selectedBackend} hardware acceleration.",
                            "Unsupported Hardware Combination", hideCancel: false, proceedText: "Use CPU instead", extraText: "Choose a different codec");
                        if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded) win.Owner = Application.Current.MainWindow;
                        win.ShowDialog();
                        choice = win.CustomResult;
                    });
                    if (choice == "Cancel" || choice == "Extra") return;
                    if (choice == "Proceed") forceCpuForJobs = true;
                }
            }
        }

        if (forceCpuForJobs)
        {
            AppSettings.HardwareBackend = "CPU";
            UpdateBackendOptions();
        }

        // Pre-flight checks for disk space and file validity
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(OutputFolder));
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                long totalSize = new FileInfo(FilePath).Length;
                if (drive.AvailableFreeSpace < totalSize * 1.5)
                {
                    bool proceed = false;
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        var win = new FormatFusion.UI.Views.ThemedDialogWindow(
                            "The destination drive (" + drive.Name + ") may not have enough free space for this operation. Proceed anyway?",
                            "Low Disk Space", hideCancel: false);
                        if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded) win.Owner = Application.Current.MainWindow;
                        proceed = win.ShowDialog() == true;
                    });
                    if (!proceed) return;
                }
            }
        }
        catch { }

        if (CategoryIcon == "🎬" || CategoryIcon == "🎵")
        {
            try 
            {
                await FFMpegCore.FFProbe.AnalyseAsync(FilePath);
            }
            catch (Exception)
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    var win = new FormatFusion.UI.Views.ThemedDialogWindow(
                        "File '" + FileName + "' appears to be corrupted or invalid and cannot be processed by FFProbe.",
                        "Invalid Media File", hideCancel: true);
                    if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded) win.Owner = Application.Current.MainWindow;
                    win.ShowDialog();
                });
                return;
            }
        }

        Directory.CreateDirectory(OutputFolder);
        var outName = Path.GetFileNameWithoutExtension(FilePath) + "_compressed" + SelectedOutputFormat;
        var outPath = Path.Combine(OutputFolder, outName);

        var job = CompressJob.Create(FilePath, outPath, targetMB, SelectedOutputFormat);
        await _orchestrator.EnqueueCompressionAsync(job);

        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        mainWindow.NavigateTo<Views.QueueView>();
    }

    [RelayCommand]
    private void ChangeFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select photo or video to compress",
            Filter = "Photos & Videos|*.jpg;*.jpeg;*.png;*.webp;*.heic;*.bmp;*.mp4;*.mkv;*.avi;*.mov;*.webm"
        };
        if (dialog.ShowDialog() == true) LoadFile(dialog.FileName);
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select output folder",
            InitialDirectory = OutputFolder,
            FileName = "Output Folder",
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false,
            Filter = "Folders|*.folder"
        };
        if (dialog.ShowDialog() == true)
            OutputFolder = System.IO.Path.GetDirectoryName(dialog.FileName) ?? OutputFolder;
    }

    [RelayCommand]
    private void GoBack()
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        mainWindow.NavigateTo<Views.HomeView>();
    }
}
