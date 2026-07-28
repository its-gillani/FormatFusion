using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatFusion.Core.Services;
using System.IO;

namespace FormatFusion.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _appSettings;

    public string DefaultOutputFolder
    {
        get => _appSettings.DefaultOutputFolder;
        set { if (_appSettings.DefaultOutputFolder != value) { _appSettings.DefaultOutputFolder = value; OnPropertyChanged(); Save(); } }
    }

    public bool OpenFolderAfterCompletion
    {
        get => _appSettings.OpenFolderAfterCompletion;
        set { if (_appSettings.OpenFolderAfterCompletion != value) { _appSettings.OpenFolderAfterCompletion = value; OnPropertyChanged(); Save(); } }
    }

    public int MaxConcurrentJobs
    {
        get => _appSettings.MaxConcurrentJobs;
        set { if (_appSettings.MaxConcurrentJobs != value) { _appSettings.MaxConcurrentJobs = value; OnPropertyChanged(); Save(); } }
    }

    public int[] AvailableConcurrentJobs { get; } = new[] { 1, 2, 4, 8 };

    public string FfmpegPath
    {
        get => _appSettings.FfmpegPath;
        set { if (_appSettings.FfmpegPath != value) { _appSettings.FfmpegPath = value; OnPropertyChanged(); Save(); } }
    }

    public string PandocPath
    {
        get => _appSettings.PandocPath;
        set { if (_appSettings.PandocPath != value) { _appSettings.PandocPath = value; OnPropertyChanged(); Save(); } }
    }

    public bool ClearTempOnExit
    {
        get => _appSettings.ClearTempOnExit;
        set { if (_appSettings.ClearTempOnExit != value) { _appSettings.ClearTempOnExit = value; OnPropertyChanged(); Save(); } }
    }

    public string AppVersion => "1.0.0";

    public SettingsViewModel(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select output folder",
            InitialDirectory = DefaultOutputFolder,
            FileName = "Output Folder",
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false,
            Filter = "Folders|*.folder"
        };
        if (dialog.ShowDialog() == true)
        {
            DefaultOutputFolder = System.IO.Path.GetDirectoryName(dialog.FileName) ?? DefaultOutputFolder;
        }
    }

    [RelayCommand]
    private void OpenLicenses()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(System.AppContext.BaseDirectory, "LICENSES.txt"),
            UseShellExecute = true
        });
    }

    private void Save()
    {
        _appSettings.Save();
    }
}
