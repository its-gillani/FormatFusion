using FormatFusion.UI.ViewModels;
using System.Windows.Controls;

namespace FormatFusion.UI.Views;

public partial class CompressorView : Page
{
    public CompressorView(CompressorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
