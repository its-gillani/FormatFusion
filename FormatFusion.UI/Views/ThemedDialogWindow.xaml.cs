using System.Windows;
using System.Windows.Input;

namespace FormatFusion.UI.Views;

public partial class ThemedDialogWindow : Window
{
    public string CustomResult { get; private set; } = "Cancel";

    public ThemedDialogWindow(string message, string title, bool hideCancel = false, string proceedText = "Proceed", string? extraText = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        
        if (hideCancel)
        {
            CancelBtn.Visibility = Visibility.Collapsed;
            ProceedBtn.Content = "OK";
        }
        else
        {
            ProceedBtn.Content = proceedText;
        }

        if (!string.IsNullOrEmpty(extraText))
        {
            ExtraBtn.Content = extraText;
            ExtraBtn.Visibility = Visibility.Visible;
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        CustomResult = "Cancel";
        DialogResult = false;
        Close();
    }

    private void ExtraBtn_Click(object sender, RoutedEventArgs e)
    {
        CustomResult = "Extra";
        DialogResult = true;
        Close();
    }

    private void ProceedBtn_Click(object sender, RoutedEventArgs e)
    {
        CustomResult = "Proceed";
        DialogResult = true;
        Close();
    }
    
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        DragMove();
    }
}
