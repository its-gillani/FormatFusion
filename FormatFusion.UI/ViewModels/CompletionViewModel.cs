using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatFusion.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace FormatFusion.UI.ViewModels;

public partial class CompletionResultViewModel : ObservableObject
{
    public string FileName { get; }
    public string OperationLabel { get; }
    public string InputSizeFormatted { get; }
    public string OutputSizeFormatted { get; }
    public string SavingsFormatted { get; }
    public string OutputPath { get; }

    public CompletionResultViewModel(JobResult result, string operation)
    {
        FileName = Path.GetFileName(result.InputPath);
        OperationLabel = operation;
        InputSizeFormatted = result.InputSizeFormatted;
        OutputSizeFormatted = result.OutputSizeFormatted;
        SavingsFormatted = $"-{result.SavingsPercent:F0}%";
        OutputPath = result.OutputPath;
    }
}

public partial class CompletionViewModel : ObservableObject
{
    [ObservableProperty] private string _headlineText = "All Done";
    [ObservableProperty] private string _totalSavedText = string.Empty;
    [ObservableProperty] private string _outputFolder = string.Empty;

    public ObservableCollection<CompletionResultViewModel> Results { get; } = new();

    public void LoadResults(IEnumerable<(JobResult Result, string Operation)> results)
    {
        Results.Clear();
        long totalSaved = 0;
        foreach (var (r, op) in results)
        {
            Results.Add(new CompletionResultViewModel(r, op));
            if (r.Success) totalSaved += r.InputSizeBytes - r.OutputSizeBytes;
        }

        HeadlineText = $"✅ All Done — {Results.Count} files processed";
        var savedMB = totalSaved / 1_048_576.0;
        TotalSavedText = $"Total saved: {savedMB:F1} MB";

        if (Results.Any()) OutputFolder = Path.GetDirectoryName(Results.First().OutputPath) ?? string.Empty;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (!string.IsNullOrEmpty(OutputFolder) && Directory.Exists(OutputFolder))
            Process.Start("explorer.exe", OutputFolder);
    }

    [RelayCommand]
    private void CopyResults()
    {
        var sb = new StringBuilder();
        sb.AppendLine("FormatFusion Conversion Results");
        sb.AppendLine("═══════════════════════════════");
        foreach (var r in Results)
            sb.AppendLine($"{r.FileName,-40} {r.InputSizeFormatted,8} → {r.OutputSizeFormatted,8} ({r.SavingsFormatted})");
        Clipboard.SetText(sb.ToString());
    }

    [RelayCommand]
    private void ConvertMore()
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        mainWindow.NavigateTo<Views.HomeView>();
    }
}
