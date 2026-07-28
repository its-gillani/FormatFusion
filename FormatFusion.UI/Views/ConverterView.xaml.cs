using FormatFusion.UI.ViewModels;
using System.Windows.Controls;

namespace FormatFusion.UI.Views;

public partial class ConverterView : Page
{
    public ConverterView(ConverterViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
