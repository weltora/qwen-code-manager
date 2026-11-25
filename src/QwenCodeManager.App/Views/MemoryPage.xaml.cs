using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.App.Views;

public sealed partial class MemoryPage : Page
{
    public MemoryPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadState();
    }

    private void LoadState()
    {
        var home = App.Services.Paths.QwenHome;
        QwenHomeText.Text = home;

        var qwenMd = Path.Combine(home, "QWEN.md");
        QwenMdText.Text = File.Exists(qwenMd)
            ? $"Найден · обновлён {File.GetLastWriteTime(qwenMd):dd.MM.yyyy HH:mm}"
            : "Не найден";

        SkillsText.Text = CountDirs(Path.Combine(home, "skills")) is var skills and >= 0
            ? $"{skills} установлено"
            : "Не найдено";

        MemoriesText.Text = Directory.Exists(Path.Combine(home, "memories")) ? "Есть" : "Не найдено";
    }

    private static int CountDirs(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.GetDirectories(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void OpenQwenMd_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(App.Services.Paths.QwenHome, "QWEN.md");
        if (File.Exists(path))
        {
            App.Services.OpenInEditor(path);
        }
    }

    private void OpenSkills_Click(object sender, RoutedEventArgs e) =>
        OpenDir(Path.Combine(App.Services.Paths.QwenHome, "skills"));

    private void OpenMemories_Click(object sender, RoutedEventArgs e) =>
        OpenDir(Path.Combine(App.Services.Paths.QwenHome, "memories"));

    private static void OpenDir(string path)
    {
        if (Directory.Exists(path))
        {
            App.Services.OpenFolder(new ProjectEntry { Name = Path.GetFileName(path), Path = path });
        }
    }
}
