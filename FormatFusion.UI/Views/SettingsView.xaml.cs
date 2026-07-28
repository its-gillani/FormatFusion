using FormatFusion.UI.ViewModels;
using System.Windows.Controls;

namespace FormatFusion.UI.Views;

public partial class SettingsView : Page
{
    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
