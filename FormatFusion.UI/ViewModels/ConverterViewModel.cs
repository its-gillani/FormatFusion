using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatFusion.Core;
using FormatFusion.Core.Interfaces;
using FormatFusion.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace FormatFusion.UI.ViewModels;

/// <summary>Per-file entry in the converter's file list.</summary>
public partial class FileEntryViewModel : ObservableObject
{
    [ObservableProperty] private string _selectedOutputFormat = string.Empty;
    [ObservableProperty] private string _selectedCodec = "Global Change";

    public int? ResizeWidth { get; set; }
    public int? ResizeHeight { get; set; }

    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string FileSizeFormatted { get; }
    public string CategoryIcon { get; }
    public IReadOnlyList<string> AvailableOutputFormats { get; }
    public bool IsVideo { get; }

    public ObservableCollection<CodecOptionViewModel> CodecOptions { get; } = new();

    public IRelayCommand RemoveCommand { get; }
    private readonly FormatFusion.Core.Services.AppSettings _appSettings;

    public FileEntryViewModel(string filePath, IFormatRegistry registry, FormatFusion.Core.Services.AppSettings appSettings, Action<FileEntryViewModel> onRemove)
    {
        FilePath = filePath;
        _appSettings = appSettings;
        FileSizeFormatted = FormatSize(new FileInfo(filePath).Length);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var cat = registry.GetCategory(ext);
        CategoryIcon = FormatFusion.UI.Helpers.IconHelper.GetIcon(cat);
        IsVideo = cat == FileCategory.Video;
        AvailableOutputFormats = registry.GetOutputFormats(ext);
        SelectedOutputFormat = AvailableOutputFormats.FirstOrDefault() ?? string.Empty;
        RemoveCommand = new RelayCommand(() => onRemove(this));

        if (IsVideo)
        {
            InitializeCodecOptions();
            _appSettings.SettingsChanged += UpdateCodecOptions;
        }
    }

    private void InitializeCodecOptions()
    {
        var codecs = new[] { "Global Change", "Default", "H.264", "H.265", "VP9", "AV1" };
        foreach (var c in codecs)
        {
            CodecOptions.Add(new CodecOptionViewModel(c));
        }
        UpdateCodecOptions();
    }

    partial void OnSelectedOutputFormatChanged(string value)
    {
        if (IsVideo) UpdateCodecOptions();
    }

    public void UpdateCodecOptions()
    {
        var backend = _appSettings.HardwareBackend;
        var caps = _appSettings.HardwareCaps;
        var targetExt = string.IsNullOrEmpty(SelectedOutputFormat) ? Path.GetExtension(FilePath).ToLowerInvariant() : "." + SelectedOutputFormat.TrimStart('.');

        foreach (var opt in CodecOptions)
        {
            FormatFusion.UI.Helpers.CompatibilityHelper.EvaluateCodecOption(opt, targetExt, backend, caps);
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{bytes / 1_024.0:F0} KB",
        _ => $"{bytes} B"
    };

    private static string GetIcon(FileCategory cat) => cat switch
    {
        FileCategory.Image => "M21 19V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2z M21 15l-5-5L5 21 M10 8.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0z",
        FileCategory.Audio => "M9 18V5l12-2v13 M6 15a3 3 0 1 0 3 3v-3H6z M18 13a3 3 0 1 0 3 3v-3h-3z",
        FileCategory.Video => "M19.8 19.8V4.2a2 2 0 0 0-2-2H6.2a2 2 0 0 0-2 2v15.6a2 2 0 0 0 2 2h11.6a2 2 0 0 0 2-2z M7 2v20 M17 2v20 M2 12h20 M2 7h5 M2 17h5 M17 17h5 M17 7h5",
        FileCategory.Document => "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M16 13H8 M16 17H8 M10 9H8",
        FileCategory.Archive => "M21 8v13H3V8 M1 3h22v5H1z M10 12h4v8h-4z",
        _ => "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6"
    };
}

public partial class ConverterViewModel : ObservableObject
{
    private readonly IFormatRegistry _registry;
    private readonly IJobOrchestrator _orchestrator;
    private readonly FormatFusion.Core.Services.AppSettings _appSettings;
    private readonly IUserPromptService _promptService;

    public FormatFusion.Core.Services.AppSettings AppSettings => _appSettings;

    [ObservableProperty] private string _outputFolder;
    [ObservableProperty] private bool _isCodecMode;
    [ObservableProperty] private bool _openAfterConversion = true;
    [ObservableProperty] private bool _overwriteExisting = false;
    [ObservableProperty] private bool _showBestEffortWarning = false;

    public ObservableCollection<CodecOptionViewModel> GlobalCodecOptions { get; } = new();
    public ObservableCollection<BackendOptionViewModel> BackendOptions { get; } = new();
    public ObservableCollection<FileEntryViewModel> Files { get; } = new();
    public bool CanConvert => Files.Count > 0 && Files.All(f => !string.IsNullOrEmpty(f.SelectedOutputFormat));

    public ConverterViewModel(IFormatRegistry registry, IJobOrchestrator orchestrator, FormatFusion.Core.Services.AppSettings appSettings, IUserPromptService promptService)
    {
        _registry = registry;
        _orchestrator = orchestrator;
        _appSettings = appSettings;
        _promptService = promptService;
        _outputFolder = _appSettings.DefaultOutputFolder;

        InitializeGlobalCodecOptions();
        UpdateBackendOptions();
        _appSettings.SettingsChanged += () => 
        {
            UpdateFileCodecOptions();
            UpdateGlobalCodecOptions();
            UpdateBackendOptions();
        };
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

    private void UpdateBackendOptions()
    {
        var caps = _appSettings.HardwareCaps;
        var options = new List<BackendOptionViewModel>
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

    [ObservableProperty] private string _conflictMessage = "";
    [ObservableProperty] private Brush? _conflictMessageBrush;
    [ObservableProperty] private bool _showConflictMessage = false;

    private void UpdateGlobalCodecOptions()
    {
        var backend = _appSettings.HardwareBackend;
        var caps = _appSettings.HardwareCaps;
        var targetExt = Files.Count > 0 
            ? (string.IsNullOrEmpty(Files[0].SelectedOutputFormat) ? Path.GetExtension(Files[0].FilePath).ToLowerInvariant() : "." + Files[0].SelectedOutputFormat.TrimStart('.'))
            : ".mp4"; // fallback default

        foreach (var opt in GlobalCodecOptions)
        {
            FormatFusion.UI.Helpers.CompatibilityHelper.EvaluateCodecOption(opt, targetExt, backend, caps);
        }
    }

    private void UpdateWarnings()
    {
        OnPropertyChanged(nameof(CanConvert));

        var activeFile = Files.FirstOrDefault();
        if (activeFile == null)
        {
            ShowConflictMessage = false;
            return;
        }

        var backend = _appSettings.HardwareBackend;
        var caps = _appSettings.HardwareCaps;
        var targetExt = string.IsNullOrEmpty(activeFile.SelectedOutputFormat) ? Path.GetExtension(activeFile.FilePath).ToLowerInvariant() : "." + activeFile.SelectedOutputFormat.TrimStart('.');
        var codec = activeFile.IsVideo ? (activeFile.SelectedCodec == "Global Change" ? _appSettings.VideoCodec : activeFile.SelectedCodec) : null;

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
        ShowBestEffortWarning = Files.Any(f =>
            Path.GetExtension(f.FilePath).ToLowerInvariant() == ".pdf"
            && f.SelectedOutputFormat?.ToLower() is "docx" or "rtf" or "odt");
    }

    private void UpdateFileCodecOptions()
    {
        foreach (var file in Files)
        {
            file.UpdateCodecOptions();
        }
    }

    public void LoadFiles(IEnumerable<string> paths)
    {
        Files.Clear();
        foreach (var path in paths.Where(File.Exists))
        {
            if (IsCodecMode && _registry.GetCategory(Path.GetExtension(path)) != FileCategory.Video)
                continue;
            
            var entry = new FileEntryViewModel(path, _registry, _appSettings, Remove);
            if (IsCodecMode)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (entry.AvailableOutputFormats.Contains(ext))
                    entry.SelectedOutputFormat = ext;
            }
            entry.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(FileEntryViewModel.SelectedOutputFormat) || e.PropertyName == nameof(FileEntryViewModel.SelectedCodec))
                {
                    if (s is FileEntryViewModel file)
                    {
                        file.UpdateCodecOptions();
                    }
                }
                UpdateWarnings();
            };
            Files.Add(entry);
        }
        UpdateWarnings();
        OnPropertyChanged(nameof(CanConvert));
    }

    private void Remove(FileEntryViewModel entry)
    {
        Files.Remove(entry);
        UpdateWarnings();
        OnPropertyChanged(nameof(CanConvert));
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
    private void AddMoreFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Add more files" };
        if (dialog.ShowDialog() == true)
        {
            foreach (var path in dialog.FileNames)
            {
                if (File.Exists(path) && Files.All(f => f.FilePath != path))
                {
                    if (IsCodecMode && _registry.GetCategory(Path.GetExtension(path)) != FileCategory.Video)
                        continue;
                        
                    var entry = new FileEntryViewModel(path, _registry, _appSettings, Remove);
                    if (IsCodecMode)
                    {
                        var ext = Path.GetExtension(path).ToLowerInvariant();
                        if (entry.AvailableOutputFormats.Contains(ext))
                            entry.SelectedOutputFormat = ext;
                    }
                    entry.PropertyChanged += (s, e) => 
                    {
                        if (e.PropertyName == nameof(FileEntryViewModel.SelectedOutputFormat) || e.PropertyName == nameof(FileEntryViewModel.SelectedCodec))
                        {
                            if (s is FileEntryViewModel file)
                            {
                                file.UpdateCodecOptions();
                            }
                        }
                        UpdateWarnings();
                    };
                    Files.Add(entry);
                }
            }
            OnPropertyChanged(nameof(CanConvert));
        }
    }

    [RelayCommand]
    private async Task Convert()
    {
        if (!CanConvert) return;

        var selectedBackend = AppSettings.HardwareBackend;
        var backendOpt = BackendOptions.FirstOrDefault(b => b.BackendName == selectedBackend);

        // Pre-flight validation for impossible combinations
        foreach (var file in Files)
        {
            var targetExt = string.IsNullOrEmpty(file.SelectedOutputFormat) ? Path.GetExtension(file.FilePath).ToLowerInvariant() : "." + file.SelectedOutputFormat.TrimStart('.');
            var resolvedCodec = file.IsVideo
                ? (file.SelectedCodec == "Global Change" ? AppSettings.VideoCodec : file.SelectedCodec)
                : null;

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
                // If they chose to fix it, just abort this run so they can fix it. Focus logic could be added here, but aborting is safe.
                return;
            }
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
                foreach (var file in Files)
                {
                    var resolvedCodec = file.IsVideo
                        ? (file.SelectedCodec == "Global Change" ? AppSettings.VideoCodec : file.SelectedCodec)
                        : null;

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
                        if (choice == "Proceed")
                        {
                            forceCpuForJobs = true;
                            break;
                        }
                    }
                }
            }
        }

        // Pre-flight check for ICO image size
        foreach (var file in Files)
        {
            if (file.SelectedOutputFormat?.TrimStart('.').ToLowerInvariant() == "ico")
            {
                try
                {
                    int width = 0, height = 0;
                    using (var stream = System.IO.File.OpenRead(file.FilePath))
                    {
                        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation, System.Windows.Media.Imaging.BitmapCacheOption.None);
                        if (decoder.Frames.Count > 0)
                        {
                            width = decoder.Frames[0].PixelWidth;
                            height = decoder.Frames[0].PixelHeight;
                        }
                    }

                    if (width > 256 || height > 256)
                    {
                        bool resize = false;
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            var win = new FormatFusion.UI.Views.ThemedDialogWindow(
                                $"This image is {width}x{height}, but .ico supports a maximum of 256x256. Resize automatically to fit, or cancel?",
                                "Oversized Icon", hideCancel: false, proceedText: "Resize & Convert");
                            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded) win.Owner = Application.Current.MainWindow;
                            resize = win.ShowDialog() == true;
                        });
                        if (!resize) return;
                        
                        file.ResizeWidth = 256;
                        file.ResizeHeight = 256;
                    }
                }
                catch { } // Let the engine catch genuine decode errors
            }
        }

        // Pre-flight checks for disk space and file validity
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(OutputFolder));
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                long totalSize = Files.Sum(f => new FileInfo(f.FilePath).Length);
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

        foreach (var file in Files)
        {
            if (file.IsVideo || file.CategoryIcon == "🎵")
            {
                try 
                {
                    await FFMpegCore.FFProbe.AnalyseAsync(file.FilePath);
                }
                catch (Exception)
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        var win = new FormatFusion.UI.Views.ThemedDialogWindow(
                            "File '" + file.FileName + "' appears to be corrupted or invalid and cannot be processed by FFProbe.",
                            "Invalid Media File", hideCancel: true);
                        if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded) win.Owner = Application.Current.MainWindow;
                        win.ShowDialog();
                    });
                    return;
                }
            }
        }

        Directory.CreateDirectory(OutputFolder);

        if (forceCpuForJobs)
        {
            AppSettings.HardwareBackend = "CPU";
            UpdateBackendOptions();
        }

        foreach (var file in Files)
        {
            var outName = Path.GetFileNameWithoutExtension(file.FilePath)
                + "_converted" + file.SelectedOutputFormat;
            var outPath = Path.Combine(OutputFolder, outName);

            if (!OverwriteExisting && File.Exists(outPath))
                outPath = GetUniquePath(outPath);

            var resolvedCodec = file.IsVideo
                ? (file.SelectedCodec == "Global Change" ? AppSettings.VideoCodec : file.SelectedCodec)
                : null;
            
            var options = new FormatFusion.Core.Models.ConversionOptions(
                VideoCodec: resolvedCodec,
                MaxImageWidth: file.ResizeWidth,
                MaxImageHeight: file.ResizeHeight
            );
            var job = ConversionJob.Create(file.FilePath, outPath, file.SelectedOutputFormat, options, OverwriteExisting);
            await _orchestrator.EnqueueConversionAsync(job);
        }

        // Navigate to queue view
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        mainWindow.NavigateTo<Views.QueueView>();
    }

    [RelayCommand]
    private void GoBack()
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        mainWindow.NavigateTo<Views.HomeView>();
    }

    private static string GetUniquePath(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        int i = 1;
        while (File.Exists(path))
            path = Path.Combine(dir, $"{name} ({i++}){ext}");
        return path;
    }
}
