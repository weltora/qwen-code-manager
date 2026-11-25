using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QwenCodeManager.App.ViewModels;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.App.Views;

public static class ProjectEditDialog
{
    public static async Task ShowAsync(ProjectViewModel vm)
    {
        var nameBox = new TextBox { Header = "Имя", Text = vm.Name };
        var descBox = new TextBox { Header = "Описание", Text = vm.Description ?? string.Empty };
        var githubBox = new TextBox { Header = "GitHub URL", Text = vm.GitHubUrl ?? string.Empty };
        var launchBox = new ComboBox
        {
            Header = "Режим запуска по умолчанию",
            Width = 240,
            ItemsSource = new[] { "Continue", "NewSession" },
            SelectedItem = vm.Entry.DefaultLaunchMode == LaunchMode.Continue ? "Continue" : "NewSession",
        };

        var panel = new StackPanel { Spacing = 14, MinWidth = 400 };
        panel.Children.Add(nameBox);
        panel.Children.Add(descBox);
        panel.Children.Add(githubBox);
        panel.Children.Add(launchBox);

        var dialog = new ContentDialog
        {
            XamlRoot = ((FrameworkElement)App.MainWindow.Content).XamlRoot,
            Title = "Изменить проект",
            Content = panel,
            PrimaryButtonText = "Сохранить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ElementTheme.Dark,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        vm.Entry.Name = nameBox.Text.Trim();
        vm.Entry.Description = string.IsNullOrWhiteSpace(descBox.Text) ? null : descBox.Text.Trim();
        vm.Entry.GitHubUrl = string.IsNullOrWhiteSpace(githubBox.Text) ? null : githubBox.Text.Trim();
        vm.Entry.DefaultLaunchMode = (string?)launchBox.SelectedItem == "NewSession"
            ? LaunchMode.NewSession
            : LaunchMode.Continue;

        await App.Services.Projects.UpdateAsync(vm.Entry);
        await vm.RefreshAsync();
    }
}
