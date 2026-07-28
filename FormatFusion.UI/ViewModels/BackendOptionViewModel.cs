using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace FormatFusion.UI.ViewModels;

public partial class BackendOptionViewModel : ObservableObject
{
    [ObservableProperty] private string _backendName = "";
    [ObservableProperty] private bool _isDetected = true;
    [ObservableProperty] private Brush? _textColor;
    [ObservableProperty] private bool _showIcon = false;
    [ObservableProperty] private string _iconGeometry = "";

    public BackendOptionViewModel(string name, bool isDetected = true)
    {
        BackendName = name;
        IsDetected = isDetected;
        TextColor = isDetected ? FormatFusion.UI.Helpers.CompatibilityHelper.GetDefaultTextBrush() : FormatFusion.UI.Helpers.CompatibilityHelper.GetHardwareConflictBrush();
        ShowIcon = !isDetected && name != "Auto (Recommended)";
        IconGeometry = FormatFusion.UI.Helpers.CompatibilityHelper.IconHardwareSwap;
    }
}
