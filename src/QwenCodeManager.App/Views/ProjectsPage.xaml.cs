using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QwenCodeManager.App.Services;
using QwenCodeManager.App.ViewModels;

namespace QwenCodeManager.App.Views;

public sealed partial class ProjectsPage : Page
{
    public MainViewModel ViewModel { get; }

    private ProjectViewModel? Selected => ViewModel.SelectedProject;

    public ProjectsPage()
    {
        InitializeComponent();
        ViewModel = new MainViewModel(App.Services);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.ReloadAsync();
    }

    private void ContinueSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.ContinueCommand.Execute(null);

    private void NewSessionSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.NewSessionCommand.Execute(null);

    private void OpenFolderSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.OpenFolderCommand.Execute(null);

    private void OpenTerminalSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.OpenTerminalCommand.Execute(null);

    private void OpenGitHubSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.OpenGitHubCommand.Execute(null);

    private void CheckpointSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.OpenCheckpointCommand.Execute(null);

    private void QwenMdSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.OpenQwenMdCommand.Execute(null);

    private void SkillsSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.OpenSkillsCommand.Execute(null);

    private void RefreshSelected_Click(object sender, RoutedEventArgs e) =>
        Selected?.RefreshCardCommand.Execute(null);

    private void ShowAllSessions_Click(object sender, RoutedEventArgs e) =>
        NavigationService.Navigate("Sessions");

    private async void EditSelected_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } vm)
        {
            await ProjectEditDialog.ShowAsync(vm);
        }
    }

    private async void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } vm)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Убрать из менеджера",
            Content = $"Убрать «{vm.Name}» из Qwen Code Manager?\nФайлы проекта на диске НЕ удаляются.",
            PrimaryButtonText = "Убрать",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ElementTheme.Dark,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await vm.RemoveCommand.ExecuteAsync(null);
        }
    }
}
