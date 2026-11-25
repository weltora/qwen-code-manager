using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwenCodeManager.App.Services;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty]
    private string _qwenExecutable = string.Empty;

    [ObservableProperty]
    private string _defaultLaunchMode = "Continue";

    [ObservableProperty]
    private string _defaultProjectsDirectory = string.Empty;

    [ObservableProperty]
    private bool _gitHubCoverLookup = true;

    [ObservableProperty]
    private bool _showHiddenProjects;

    [ObservableProperty]
    private int _coverCacheDays = 14;

    [ObservableProperty]
    private string _detectedQwenText = string.Empty;

    [ObservableProperty]
    private string _logsDirectory = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public string[] LaunchModes { get; } = ["Продолжить", "Новая сессия"];

    public string QwenHomePath => _services.Paths.QwenHome;
    public bool QwenMdFound => File.Exists(Path.Combine(_services.Paths.QwenHome, "QWEN.md"));
    public int SkillsCount
    {
        get
        {
            try
            {
                var skills = Path.Combine(_services.Paths.QwenHome, "skills");
                return Directory.Exists(skills) ? Directory.GetDirectories(skills).Length : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    [RelayCommand]
    private void OpenQwenMd()
    {
        var path = Path.Combine(_services.Paths.QwenHome, "QWEN.md");
        if (File.Exists(path))
        {
            _services.OpenInEditor(path);
        }
    }

    [RelayCommand]
    private void OpenSkillsFolder()
    {
        var path = Path.Combine(_services.Paths.QwenHome, "skills");
        if (Directory.Exists(path))
        {
            _services.OpenFolder(new ProjectEntry { Name = "Skills", Path = path });
        }
    }

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        Load();
    }

    private void Load()
    {
        var s = _services.Settings.Settings;
        QwenExecutable = s.QwenExecutable ?? string.Empty;
        DefaultLaunchMode = s.DefaultLaunchMode == LaunchMode.Continue ? "Продолжить" : "Новая сессия";
        DefaultProjectsDirectory = s.DefaultProjectsDirectory ?? string.Empty;
        GitHubCoverLookup = s.GitHubCoverLookup;
        ShowHiddenProjects = s.ShowHiddenProjects;
        CoverCacheDays = s.CoverCacheDays;
        LogsDirectory = _services.Paths.Logs;
        DetectedQwenText = _services.QwenInfo.Found
            ? $"{_services.QwenInfo.ExecutablePath} (версия {_services.QwenInfo.Version})"
            : "Не найден";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _services.Settings.UpdateAsync(s =>
        {
            s.QwenExecutable = string.IsNullOrWhiteSpace(QwenExecutable) ? null : QwenExecutable.Trim();
            s.DefaultLaunchMode = DefaultLaunchMode == "Продолжить" ? LaunchMode.Continue : LaunchMode.NewSession;
            s.DefaultProjectsDirectory = string.IsNullOrWhiteSpace(DefaultProjectsDirectory) ? null : DefaultProjectsDirectory.Trim();
            s.GitHubCoverLookup = GitHubCoverLookup;
            s.ShowHiddenProjects = ShowHiddenProjects;
            s.CoverCacheDays = Math.Clamp(CoverCacheDays, 1, 365);
        });

        _services.Qwen.CurrentExecutableOverride = _services.Settings.Settings.QwenExecutable;
        StatusMessage = "Настройки сохранены.";
    }

    [RelayCommand]
    private async Task AutoDetectQwenAsync()
    {
        _services.Qwen.CurrentExecutableOverride = null;
        _services.QwenInfo = await _services.Qwen.DetectAsync();
        if (_services.QwenInfo.Found)
        {
            QwenExecutable = _services.QwenInfo.ExecutablePath ?? string.Empty;
            DetectedQwenText = $"{_services.QwenInfo.ExecutablePath} (версия {_services.QwenInfo.Version})";
            StatusMessage = "Qwen Code обнаружен.";
        }
        else
        {
            DetectedQwenText = "Не найден";
            StatusMessage = "Qwen Code не найден. Укажите путь к qwen.cmd вручную.";
        }
    }

    [RelayCommand]
    private async Task PickQwenExecutableAsync()
    {
        var path = await FilePicker.PickAsync([".cmd", ".exe", ".bat"]);
        if (path is not null)
        {
            QwenExecutable = path;
            await SaveAsync();
        }
    }

    [RelayCommand]
    private async Task PickProjectsDirectoryAsync()
    {
        var path = await FolderPicker.PickAsync();
        if (path is not null)
        {
            DefaultProjectsDirectory = path;
        }
    }

    [RelayCommand]
    private void OpenLogs() => _services.OpenFolder(new ProjectEntry { Path = LogsDirectory, Name = "Logs" });

    [RelayCommand]
    private void ClearCache()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_services.Paths.Covers))
            {
                File.Delete(file);
            }

            StatusMessage = "Кеш обложек очищен.";
            _services.Logger.Info("Cover cache cleared by user.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Не удалось очистить кеш: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        await _services.Settings.ResetAsync();
        Load();
        StatusMessage = "Настройки сброшены (резервная копия сохранена).";
    }
}
