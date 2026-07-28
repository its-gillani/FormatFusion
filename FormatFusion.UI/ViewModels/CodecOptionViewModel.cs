using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace FormatFusion.UI.ViewModels;

public partial class CodecOptionViewModel : ObservableObject
{
    [ObservableProperty] private string _codecName = "";
    [ObservableProperty] private Brush? _textColor;
    [ObservableProperty] private string _warningMessage = "";
    [ObservableProperty] private bool _showIcon = false;
    [ObservableProperty] private string _iconGeometry = "";

    public CodecOptionViewModel(string name)
    {
        CodecName = name;
        TextColor = FormatFusion.UI.Helpers.CompatibilityHelper.GetDefaultTextBrush();
    }
}
