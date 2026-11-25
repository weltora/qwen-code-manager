using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using QwenCodeManager.App.Services;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.App.ViewModels;

public partial class ProjectViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ProjectEntry Entry { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _path;

    [ObservableProperty]
    private string _typeName = "Other";

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _gitHubUrl;

    [ObservableProperty]
    private string _statusText = "…";

    [ObservableProperty]
    private string _statusBrushKey = "TextTertiaryBrush";

    [ObservableProperty]
    private string _gitSummary = "Git: unknown";

    [ObservableProperty]
    private string _lastSessionText = "Not available";

    [ObservableProperty]
    private string _lastActivityText = "Unknown";

    [ObservableProperty]
    private bool _canContinue;

    [ObservableProperty]
    private BitmapImage? _cover;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _lastLaunchText = "Нет данных";

    [ObservableProperty]
    private string _durationText = "Неизвестно";

    [ObservableProperty]
    private string _sessionStateText = "Нет сессий";

    [ObservableProperty]
    private string _sessionStateBrushKey = "TextTertiaryBrush";

    [ObservableProperty]
    private bool _hasContinueBanner;

    [ObservableProperty]
    private string _continueBannerText = string.Empty;

    [ObservableProperty]
    private string _memoryText = "Не найдена";

    [ObservableProperty]
    private string _memoryUpdatedText = string.Empty;

    [ObservableProperty]
    private string _typeIconGlyph = "\uE8A5";

    public System.Collections.ObjectModel.ObservableCollection<SessionRowViewModel> Sessions { get; } = new();

    public Microsoft.UI.Xaml.Media.Brush SessionStateBrush =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[SessionStateBrushKey];

    partial void OnSessionStateBrushKeyChanged(string value) => OnPropertyChanged(nameof(SessionStateBrush));

    public int StatusSortKey { get; private set; }
    public int GitChangeCount { get; private set; }
    public DateTime? LastActivity { get; private set; }

    public string GitHubButtonLabel => "Open GitHub";

    public ProjectViewModel(AppServices services, ProjectEntry entry)
    {
        _services = services;
        Entry = entry;
        _name = entry.Name;
        _path = entry.Path;
        _description = entry.Description;
        _gitHubUrl = entry.GitHubUrl;
    }

    public async Task RefreshAsync()
    {
        try
        {
            var state = await Task.Run(() =>
                _services.Projects.RefreshStateAsync(Entry, _services.QwenInfo.Found)).ConfigureAwait(true);

            TypeName = state.Type.DisplayName();
            if (string.IsNullOrWhiteSpace(Description) && state.DetectedName is { } detected && detected != Name)
            {
                // keep user name; detection name is only informational
            }

            if (string.IsNullOrWhiteSpace(GitHubUrl) && state.DetectedGitHubUrl is { } url)
            {
                GitHubUrl = url;
                Entry.GitHubUrl = url;
                await _services.Projects.SaveAsync().ConfigureAwait(true);
            }

            ApplyStatus(state);
            ApplyGit(state.Git);
            ApplySession(state.LastSession);
            ApplyTypeIcon(state.Type);
            ApplyMemory();
            LoadSessions();

            var coverPath = await _services.Covers.ResolveAsync(
                Entry, _services.Settings.Settings.GitHubCoverLookup).ConfigureAwait(true);
            if (coverPath is not null)
            {
                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(coverPath);
                Cover = bitmap;
            }

            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            _services.Logger.Error($"Card refresh failed for '{Name}'", ex);
            ErrorMessage = ex.Message;
            StatusText = "Error";
            StatusBrushKey = "ErrRedBrush";
        }
    }

    private void ApplyStatus(ProjectState state)
    {
        (StatusText, StatusBrushKey, StatusSortKey) = state.Status switch
        {
            ProjectStatus.Ready => ("Ready", "OkGreenBrush", 0),
            ProjectStatus.GitModified => ("Git modified", "WarnAmberBrush", 1),
            ProjectStatus.NoGit => ("No Git", "TextTertiaryBrush", 2),
            ProjectStatus.QwenUnavailable => ("Qwen unavailable", "ErrRedBrush", 3),
            ProjectStatus.MissingPath => ("Path missing", "ErrRedBrush", 4),
            ProjectStatus.Working => ("Working", "TealAccentBrush", 0),
            ProjectStatus.NeedsAttention => ("Needs attention", "WarnAmberBrush", 1),
            _ => ("Error", "ErrRedBrush", 5),
        };
        OnPropertyChanged(nameof(StatusBrush));
    }

    public Microsoft.UI.Xaml.Media.Brush StatusBrush =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[StatusBrushKey];

    partial void OnStatusBrushKeyChanged(string value) => OnPropertyChanged(nameof(StatusBrush));

    private void ApplyGit(GitStatus? git)
    {
        if (git is not { IsRepository: true })
        {
            GitSummary = "No Git repository";
            GitChangeCount = 0;
            return;
        }

        GitChangeCount = git.Modified + git.Staged + git.Untracked;
        var parts = new List<string>();
        if (git.Branch is { Length: > 0 } branch)
        {
            parts.Add(branch);
        }

        if (git.IsClean && git.Ahead == 0 && git.Behind == 0)
        {
            parts.Add("clean");
        }
        else
        {
            if (git.Modified > 0) parts.Add($"{git.Modified} modified");
            if (git.Staged > 0) parts.Add($"{git.Staged} staged");
            if (git.Untracked > 0) parts.Add($"{git.Untracked} untracked");
            if (git.Ahead > 0) parts.Add($"ahead {git.Ahead}");
            if (git.Behind > 0) parts.Add($"behind {git.Behind}");
        }

        GitSummary = string.Join(" · ", parts);
    }

    private void ApplySession(QwenSessionSummary? summary)
    {
        if (summary?.Last is { } last)
        {
            CanContinue = summary.CanContinue;
            LastSessionText = FormatWhen(last.StartedAtUtc ?? last.LastActivityUtc);
            LastLaunchText = LastSessionText;
            LastActivity = last.LastActivityUtc ?? last.StartedAtUtc;
            LastActivityText = last.Title ?? last.FirstPrompt ?? "Нет данных";
            DurationText = FormatDuration(last.StartedAtUtc, last.LastActivityUtc);

            (SessionStateText, SessionStateBrushKey) = last.IsRunning
                ? ("Активна", "TealAccentBrush")
                : last.HasTranscript ? ("Записана", "OkGreenBrush") : ("Только метаданные", "TextTertiaryBrush");

            HasContinueBanner = summary.CanContinue && !last.IsRunning;
            ContinueBannerText = last.Title ?? last.FirstPrompt ?? "Последняя сессия";
        }
        else
        {
            CanContinue = false;
            LastSessionText = "Нет сессий";
            LastLaunchText = "Нет данных";
            LastActivityText = "Неизвестно";
            LastActivity = null;
            DurationText = "Неизвестно";
            SessionStateText = "Нет сессий";
            SessionStateBrushKey = "TextTertiaryBrush";
            HasContinueBanner = false;
        }
    }

    private void ApplyTypeIcon(Core.Models.ProjectType type)
    {
        TypeIconGlyph = type switch
        {
            Core.Models.ProjectType.Android => "\uE8EA",
            Core.Models.ProjectType.DotNet => "\uE943",
            Core.Models.ProjectType.Web => "\uE774",
            Core.Models.ProjectType.NodeJs => "\uE943",
            Core.Models.ProjectType.Python => "\uE943",
            Core.Models.ProjectType.Java or Core.Models.ProjectType.Kotlin => "\uE7B8",
            Core.Models.ProjectType.Rust => "\uE950",
            Core.Models.ProjectType.Cpp => "\uE943",
            Core.Models.ProjectType.Unity or Core.Models.ProjectType.Unreal => "\uE7FC",
            _ => "\uE8A5",
        };
    }

    private void ApplyMemory()
    {
        try
        {
            var qwenMd = System.IO.Path.Combine(_services.Paths.QwenHome, "QWEN.md");
            if (File.Exists(qwenMd))
            {
                MemoryText = "Есть";
                MemoryUpdatedText = $"Обновлено: {File.GetLastWriteTime(qwenMd):dd.MM HH:mm}";
            }
            else
            {
                MemoryText = "Не найдена";
                MemoryUpdatedText = string.Empty;
            }
        }
        catch
        {
            MemoryText = "Неизвестно";
        }
    }

    private void LoadSessions()
    {
        Sessions.Clear();
        try
        {
            foreach (var session in _services.Sessions.ListSessions(Entry.Path).Take(8))
            {
                Sessions.Add(new SessionRowViewModel(_services, Entry, session));
            }
        }
        catch (Exception ex)
        {
            _services.Logger.Warn($"Session list failed for '{Name}': {ex.Message}");
        }
    }

    internal static string FormatDuration(DateTime? startUtc, DateTime? endUtc)
    {
        if (startUtc is { } start && endUtc is { } end && end > start)
        {
            var span = end - start;
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours} ч {span.Minutes} мин";
            }

            return span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} мин" : "<1 мин";
        }

        return "Неизвестно";
    }

    internal static string FormatWhen(DateTime? utc)
    {
        if (utc is not { } value)
        {
            return "Unknown";
        }

        var local = value.ToLocalTime();
        var today = DateTime.Today;
        if (local.Date == today)
        {
            return $"Сегодня, {local:HH:mm}";
        }

        if (local.Date == today.AddDays(-1))
        {
            return $"Вчера, {local:HH:mm}";
        }

        return local.ToString("dd MMM, HH:mm", new System.Globalization.CultureInfo("ru-RU"));
    }

    // ---- Commands ----

    [RelayCommand]
    private async Task ContinueAsync()
    {
        var result = await _services.Qwen.LaunchAsync(Entry, LaunchMode.Continue);
        if (!result.Ok)
        {
            ErrorMessage = result.Error;
        }
        else
        {
            Entry.LastOpenedUtc = DateTime.UtcNow;
            await _services.Projects.SaveAsync();
        }
    }

    [RelayCommand]
    private async Task NewSessionAsync()
    {
        var result = await _services.Qwen.LaunchAsync(Entry, LaunchMode.NewSession);
        if (!result.Ok)
        {
            ErrorMessage = result.Error;
        }
        else
        {
            Entry.LastOpenedUtc = DateTime.UtcNow;
            await _services.Projects.SaveAsync();
        }
    }

    [RelayCommand]
    private void OpenFolder() => _services.OpenFolder(Entry);

    [RelayCommand]
    private void OpenTerminal() => _services.OpenTerminal(Entry);

    [RelayCommand]
    private void OpenGitHub()
    {
        if (GitHubUrl is { Length: > 0 } url)
        {
            _services.OpenUrl(url);
        }
    }

    [RelayCommand]
    private Task RefreshCardAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RemoveAsync()
    {
        await _services.Projects.RemoveAsync(Entry.Id);
    }

    [RelayCommand]
    private void OpenQwenMd()
    {
        var path = System.IO.Path.Combine(_services.Paths.QwenHome, "QWEN.md");
        if (File.Exists(path))
        {
            _services.OpenInEditor(path);
        }
    }

    [RelayCommand]
    private void OpenSkills()
    {
        var path = System.IO.Path.Combine(_services.Paths.QwenHome, "skills");
        if (Directory.Exists(path))
        {
            _services.OpenFolder(new ProjectEntry { Name = "Skills", Path = path });
        }
    }

    [RelayCommand]
    private async Task OpenCheckpointAsync()
    {
        try
        {
            var path = await _services.Checkpoints.CreateIfMissingAsync(Entry);
            _services.OpenInEditor(path);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
