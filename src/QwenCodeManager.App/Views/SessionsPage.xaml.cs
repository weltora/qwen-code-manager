using Microsoft.UI.Xaml.Controls;
using QwenCodeManager.App.ViewModels;

namespace QwenCodeManager.App.Views;

public sealed partial class SessionsPage : Page
{
    public SessionsViewModel ViewModel { get; }

    public SessionsPage()
    {
        InitializeComponent();
        ViewModel = new SessionsViewModel(App.Services);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }
}
