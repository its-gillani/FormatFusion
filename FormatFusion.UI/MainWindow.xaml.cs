using FormatFusion.UI.ViewModels;
using FormatFusion.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace FormatFusion.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        
        NavigateTo<HomeView>();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = App.Services.GetRequiredService<FormatFusion.Core.Services.AppSettings>();
        
        // Restore bounds
        if (!double.IsNaN(settings.WindowWidth) && settings.WindowWidth > 0)
            this.Width = settings.WindowWidth;
        if (!double.IsNaN(settings.WindowHeight) && settings.WindowHeight > 0)
            this.Height = settings.WindowHeight;

        // Restore position, checking for off-screen bounds
        if (!double.IsNaN(settings.WindowLeft) && !double.IsNaN(settings.WindowTop))
        {
            var left = settings.WindowLeft;
            var top = settings.WindowTop;

            if (left < SystemParameters.VirtualScreenLeft || 
                top < SystemParameters.VirtualScreenTop || 
                left + this.Width > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth || 
                top + this.Height > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
            {
                // Off-screen, fall back to center screen
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                this.Left = left;
                this.Top = top;
            }
        }
        else
        {
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (settings.WindowMaximized)
            this.WindowState = WindowState.Maximized;
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = App.Services.GetRequiredService<FormatFusion.Core.Services.AppSettings>();
        
        if (this.WindowState == WindowState.Maximized)
        {
            settings.WindowMaximized = true;
            settings.WindowWidth = this.RestoreBounds.Width;
            settings.WindowHeight = this.RestoreBounds.Height;
            settings.WindowLeft = this.RestoreBounds.Left;
            settings.WindowTop = this.RestoreBounds.Top;
        }
        else
        {
            settings.WindowMaximized = false;
            settings.WindowWidth = this.Width;
            settings.WindowHeight = this.Height;
            settings.WindowLeft = this.Left;
            settings.WindowTop = this.Top;
        }
        
        settings.Save();
    }

    public void NavigateTo<T>() where T : System.Windows.Controls.Page
    {
        var page = App.Services.GetRequiredService<T>();
        MainFrame.Navigate(page);
    }

    private void NavHome_Click(object sender, RoutedEventArgs e)
        => NavigateTo<HomeView>();

    private void NavQueue_Click(object sender, RoutedEventArgs e)
        => NavigateTo<QueueView>();

    private void NavSettings_Click(object sender, RoutedEventArgs e)
        => NavigateTo<SettingsView>();

    private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        var settings = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<FormatFusion.Core.Services.AppSettings>(App.Services);
        var folder = settings.DefaultOutputFolder;
        if (System.IO.Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    private void SwitchTheme(string themeFile)
    {
        var dict = new ResourceDictionary { Source = new Uri($"Theme/{themeFile}", UriKind.Relative) };
        var merged = Application.Current.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Source != null && merged[i].Source.ToString().Contains("Colors"))
            {
                merged.RemoveAt(i);
            }
        }
        merged.Insert(0, dict);
    }

    private void DarkModeToggle_Checked(object sender, RoutedEventArgs e)
        => SwitchTheme("ColorsDark.xaml");

    private void DarkModeToggle_Unchecked(object sender, RoutedEventArgs e)
        => SwitchTheme("ColorsLight.xaml");

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
