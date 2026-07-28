using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatFusion.Core.Models;
using FormatFusion.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace FormatFusion.UI.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly RecentJobsService _recentJobsService;
    private readonly IServiceProvider _services;

    [ObservableProperty] private bool _isDragOver;
    public ObservableCollection<RecentJobRecord> RecentJobs { get; } = new();

    // Files staged from drop zone before navigating to a mode
    public List<string> StagedFiles { get; private set; } = new();

    public HomeViewModel(RecentJobsService recentJobsService, IServiceProvider services)
    {
        _recentJobsService = recentJobsService;
        _services = services;
    }

    public async Task LoadRecentJobsAsync()
    {
        var records = await _recentJobsService.LoadAsync();
        RecentJobs.Clear();
        foreach (var r in records.Take(10)) RecentJobs.Add(r);
    }

    public void HandleFilesForFormat(IEnumerable<string> files)
    {
        StagedFiles = files.ToList();
        NavigateToConvert(isCodecMode: false);
    }

    public void HandleFilesForCodec(IEnumerable<string> files)
    {
        StagedFiles = files.ToList();
        NavigateToConvert(isCodecMode: true);
    }

    public void HandleFilesForCompress(IEnumerable<string> files)
    {
        StagedFiles = files.ToList();
        NavigateToCompress();
    }

    private void NavigateToConvert(bool isCodecMode)
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        var view = App.Services.GetRequiredService<Views.ConverterView>();
        var vm = (ConverterViewModel)view.DataContext;
        vm.IsCodecMode = isCodecMode;
        if (StagedFiles.Any())
        {
            vm.LoadFiles(StagedFiles);
        }
        mainWindow.MainFrame.Navigate(view);
    }

    private void NavigateToCompress()
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        var view = App.Services.GetRequiredService<Views.CompressorView>();
        var vm = (CompressorViewModel)view.DataContext;
        if (StagedFiles.Any())
        {
            vm.LoadFile(StagedFiles.First());
        }
        mainWindow.MainFrame.Navigate(view);
    }
}
