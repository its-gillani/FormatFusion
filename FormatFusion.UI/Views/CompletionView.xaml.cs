using FormatFusion.UI.ViewModels;
using System.Windows.Controls;

namespace FormatFusion.UI.Views;

public partial class CompletionView : Page
{
    public CompletionView(CompletionViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
