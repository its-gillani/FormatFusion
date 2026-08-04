using FormatFusion.UI.ViewModels;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace FormatFusion.UI.Views;

public partial class SettingsView : Page
{
    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
