using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QwenCodeManager.App.Services;
using QwenCodeManager.App.ViewModels;
using QwenCodeManager.App.Views;

namespace QwenCodeManager.App;

public sealed partial class MainWindow : Window
{
    public ShellViewModel Shell { get; }

    public Microsoft.UI.Xaml.Media.Brush QwenDotBrush(bool found) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[found ? "OkGreenBrush" : "ErrRedBrush"];

    public Microsoft.UI.Xaml.Media.Brush MemoryBrush(bool available) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[available ? "OkGreenBrush" : "TextTertiaryBrush"];

    public string QwenReadyText(bool found) => found ? "Готов к запуску" : "Не найден";

    public MainWindow()
    {
        InitializeComponent();
        Shell = new ShellViewModel(App.Services);

        Title = "Qwen Code Manager";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowSize.SetMinSize(hwnd, 900, 600);
        WindowSize.Resize(hwnd, 1280, 840);

        ContentFrame.Navigate(typeof(ProjectsPage));
        NavProjects.IsChecked = true;
        RootGrid.Loaded += MainWindow_Loaded;
        NavigationService.NavigateRequested += NavigateTo;
        Closed += (_, _) => NavigationService.NavigateRequested -= NavigateTo;
    }

    private void NavigateTo(string tag)
    {
        var button = tag switch
        {
            "Sessions" => NavSessions,
            "Memory" => NavMemory,
            "Settings" => NavSettings,
            "About" => NavAbout,
            _ => NavProjects,
        };
        button.IsChecked = true;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (ContentFrame is null || sender is not RadioButton button || button.IsChecked != true)
        {
            return;
        }

        var page = (button.Tag as string) switch
        {
            "Sessions" => typeof(SessionsPage),
            "Memory" => typeof(MemoryPage),
            "Settings" => typeof(SettingsPage),
            "About" => typeof(AboutPage),
            _ => typeof(ProjectsPage),
        };

        if (ContentFrame.CurrentSourcePageType != page)
        {
            ContentFrame.Navigate(page);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= MainWindow_Loaded;
        await App.Services.InitializeAsync();
        await Shell.RefreshAsync();

        if (!App.Services.Settings.Settings.OnboardingCompleted)
        {
            await ShowOnboardingAsync();
        }
    }

    private async Task ShowOnboardingAsync()
    {
        var qwenText = new TextBlock
        {
            Text = App.Services.QwenInfo.Found
                ? $"Qwen Code обнаружен: {App.Services.QwenInfo.Version}\n{App.Services.QwenInfo.ExecutablePath}"
                : "Qwen Code не найден. Путь к qwen.cmd можно указать позже в настройках.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        };

        var panel = new StackPanel { Spacing = 14, MinWidth = 440 };
        panel.Children.Add(new TextBlock
        {
            Text = "Добавьте папку с проектами или пропустите — проекты можно добавить позже кнопкой «Добавить».",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });
        panel.Children.Add(qwenText);

        var pickButton = new Button { Content = "Выбрать папку проектов…" };
        pickButton.Click += async (_, _) =>
        {
            var folder = await FolderPicker.PickAsync();
            if (folder is not null)
            {
                await App.Services.Settings.UpdateAsync(s => s.DefaultProjectsDirectory = folder);
                qwenText.Text += $"\nПапка проектов: {folder}";
            }
        };
        panel.Children.Add(pickButton);

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Qwen Code Manager",
            Content = panel,
            PrimaryButtonText = "Продолжить",
            CloseButtonText = "Пропустить",
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ElementTheme.Dark,
        };

        await dialog.ShowAsync();
        await App.Services.Settings.UpdateAsync(s => s.OnboardingCompleted = true);
    }
}
