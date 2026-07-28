using FormatFusion.UI.ViewModels;
using System.Windows.Controls;

namespace FormatFusion.UI.Views;

public partial class QueueView : Page
{
    public QueueView(QueueViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
