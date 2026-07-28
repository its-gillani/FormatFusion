using CommunityToolkit.Mvvm.ComponentModel;

namespace FormatFusion.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _currentView = "Home";
}
