using Microsoft.UI.Xaml.Controls;
using QwenCodeManager.App.ViewModels;

namespace QwenCodeManager.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(App.Services);
        DataContext = ViewModel;
    }
}
