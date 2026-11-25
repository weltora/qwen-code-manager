using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.App.ViewModels;

/// <summary>
/// Stable binding surface for the detail panel: forwards change notifications from
/// whichever ProjectViewModel is currently selected, so x:Bind paths never go null.
/// </summary>
public sealed class ProjectDetailViewModel : ObservableObject
{
    private static readonly ObservableCollection<SessionRowViewModel> EmptySessions = new();
    private ProjectViewModel? _source;

    public ProjectViewModel? Source => _source;
    public ProjectEntry? Entry => _source?.Entry;

    public void Attach(ProjectViewModel? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return;
        }

        if (_source is not null)
        {
            _source.PropertyChanged -= OnSourceChanged;
        }

        _source = source;
        if (_source is not null)
        {
            _source.PropertyChanged += OnSourceChanged;
        }

        OnPropertyChanged(string.Empty);
    }

    private void OnSourceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        OnPropertyChanged(e.PropertyName);

    public string Name => _source?.Name ?? string.Empty;
    public string Path => _source?.Path ?? string.Empty;
    public string TypeName => _source?.TypeName ?? "Other";
    public string? Description => _source?.Description;
    public string TypeIconGlyph => _source?.TypeIconGlyph ?? "\uE8A5";
    public string StatusText => _source?.StatusText ?? "…";
    public Microsoft.UI.Xaml.Media.Brush StatusBrush => _source?.StatusBrush
        ?? (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextTertiaryBrush"];
    public string GitSummary => _source?.GitSummary ?? string.Empty;
    public string LastLaunchText => _source?.LastLaunchText ?? "Нет данных";
    public string DurationText => _source?.DurationText ?? "Неизвестно";
    public string SessionStateText => _source?.SessionStateText ?? "Нет сессий";
    public Microsoft.UI.Xaml.Media.Brush SessionStateBrush => _source?.SessionStateBrush ?? StatusBrush;
    public string MemoryText => _source?.MemoryText ?? "Неизвестно";
    public string MemoryUpdatedText => _source?.MemoryUpdatedText ?? string.Empty;
    public bool HasContinueBanner => _source?.HasContinueBanner ?? false;
    public string ContinueBannerText => _source?.ContinueBannerText ?? string.Empty;
    public bool CanContinue => _source?.CanContinue ?? false;
    public string? GitHubUrl => _source?.GitHubUrl;
    public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? Cover => _source?.Cover;
    public int SessionCount => Sessions.Count;
    public ObservableCollection<SessionRowViewModel> Sessions => _source?.Sessions ?? EmptySessions;
}
