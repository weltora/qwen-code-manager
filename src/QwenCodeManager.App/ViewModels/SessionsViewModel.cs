using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwenCodeManager.App.Services;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.App.ViewModels;

public partial class SessionsViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isEmpty;

    public ObservableCollection<SessionRowViewModel> Rows { get; } = new();

    public SessionsViewModel(AppServices services) => _services = services;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var rows = new List<SessionRowViewModel>();
            var projects = _services.Projects.Projects.ToList();

            await Task.Run(() =>
            {
                foreach (var project in projects)
                {
                    try
                    {
                        foreach (var session in _services.Sessions.ListSessions(project.Path))
                        {
                            rows.Add(new SessionRowViewModel(_services, project, session));
                        }
                    }
                    catch (Exception ex)
                    {
                        _services.Logger.Warn($"Session scan failed for '{project.Name}': {ex.Message}");
                    }
                }
            }).ConfigureAwait(true);

            Rows.Clear();
            foreach (var row in rows.OrderByDescending(r => r.Activity ?? DateTime.MinValue).Take(200))
            {
                Rows.Add(row);
            }

            IsEmpty = Rows.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class SessionRowViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly ProjectEntry _project;
    private readonly QwenSession _session;

    public string ProjectName => _project.Name;
    public string SessionId => _session.SessionId;
    public string DateText { get; }
    public string StatusText { get; }
    public string ActivityText { get; }
    public string DurationText { get; }
    public DateTime? Activity { get; }
    public bool CanResume => _session.HasTranscript;
    public string StatusBrushKey { get; }
    public bool IsRunning => _session.IsRunning;

    public Microsoft.UI.Xaml.Media.Brush StatusBrush =>
        (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources[StatusBrushKey];

    public Microsoft.UI.Xaml.Media.Brush PillBackground => StatusBrushKey switch
    {
        "OkGreenBrush" => (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["GreenSoftBrush"],
        "TealAccentBrush" => (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TealSoftBrush"],
        _ => (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["BgElevatedBrush"],
    };

    public SessionRowViewModel(AppServices services, ProjectEntry project, QwenSession session)
    {
        _services = services;
        _project = project;
        _session = session;
        Activity = session.LastActivityUtc ?? session.StartedAtUtc;
        DateText = ProjectViewModel.FormatWhen(session.StartedAtUtc);
        ActivityText = session.Title ?? session.FirstPrompt ?? "Нет данных";
        (StatusText, StatusBrushKey) = session.IsRunning
            ? ("Активна", "TealAccentBrush")
            : session.HasTranscript ? ("Записана", "OkGreenBrush") : ("Только метаданные", "TextTertiaryBrush");
        DurationText = ProjectViewModel.FormatDuration(session.StartedAtUtc, session.LastActivityUtc);
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        var result = await _services.Qwen.LaunchAsync(_project, LaunchMode.Continue, _session.SessionId);
        if (!result.Ok)
        {
            _services.Logger.Warn($"Session resume failed: {result.Error}");
        }
    }

    [RelayCommand]
    private void OpenProject() => _services.OpenFolder(_project);

    [RelayCommand]
    private void ViewMetadata()
    {
        var chats = _services.Sessions.ResolveChatsDir(_project.Path);
        var runtime = Path.Combine(chats, _session.SessionId + ".runtime.json");
        if (File.Exists(runtime))
        {
            _services.OpenInEditor(runtime);
        }
    }
}
