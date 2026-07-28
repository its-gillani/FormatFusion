using FormatFusion.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace FormatFusion.UI.Views;

public partial class HomeView : Page
{
    private readonly HomeViewModel _vm;

    public HomeView(HomeViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        _ = vm.LoadRecentJobsAsync();
    }

    private void ConvertFormat_FilesDropped(object sender, string[] files)
    {
        _vm.HandleFilesForFormat(files);
    }

    private void ConvertCodec_FilesDropped(object sender, string[] files)
    {
        _vm.HandleFilesForCodec(files);
    }

    private void Compress_FilesDropped(object sender, string[] files)
    {
        _vm.HandleFilesForCompress(files);
    }
}
