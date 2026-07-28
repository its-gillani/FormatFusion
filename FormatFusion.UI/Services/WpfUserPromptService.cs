using FormatFusion.Core.Interfaces;
using FormatFusion.UI.Views;
using System.Windows;

namespace FormatFusion.UI.Services;

public class WpfUserPromptService : IUserPromptService
{
    public bool PromptUser(string message, string title)
    {
        var result = Application.Current.Dispatcher.Invoke(() =>
        {
            var window = new ThemedDialogWindow(message, title)
            {
                Owner = Application.Current.MainWindow
            };
            return window.ShowDialog() == true;
        });

        return result;
    }
}
