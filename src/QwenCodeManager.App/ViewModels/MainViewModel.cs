using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QwenCodeManager.App.Services;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _sortMode = "Недавние";

    [ObservableProperty]
    private string _filterType = "Все";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ProjectViewModel? _selectedProject;

    public ProjectDetailViewModel Detail { get; } = new();

    public bool HasSelection => SelectedProject is not null;

    partial void OnSelectedProjectChanged(ProjectViewModel? value)
    {
        Detail.Attach(value);
        OnPropertyChanged(nameof(HasSelection));
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public ObservableCollection<ProjectViewModel> Projects { get; } = new();

    public string[] SortOptions { get; } = ["Недавние", "Имя", "Статус", "Git активность"];
    public string[] FilterOptions { get; } = ["Все", "Android", "Web", ".NET", "Другие"];

    public MainViewModel(AppServices services)
    {
        _services = services;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _services.Projects.ProjectsChanged += () => _dispatcher.TryEnqueue(() => _ = ReloadAsync());
    }

    partial void OnSearchTextChanged(string value) => ApplyView();
    partial void OnSortModeChanged(string value) => ApplyView();
    partial void OnFilterTypeChanged(string value) => ApplyView();

    private List<ProjectViewModel> _all = new();

    [RelayCommand]
    public async Task ReloadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var entries = _services.Projects.Projects
                .Where(p => _services.Settings.Settings.ShowHiddenProjects || !p.Hidden)
                .ToList();

            var viewModels = new List<ProjectViewModel>();
            foreach (var entry in entries)
            {
                var vm = new ProjectViewModel(_services, entry);
                viewModels.Add(vm);
            }

            _all = viewModels;
            ApplyView();

            // Scan live state off the UI thread; update cards as results arrive.
            await Task.WhenAll(viewModels.Select(vm => vm.RefreshAsync())).ConfigureAwait(true);
            ApplyView();
        }
        catch (Exception ex)
        {
            _services.Logger.Error("Project reload failed", ex);
            ErrorMessage = "Не удалось загрузить проекты. Подробности в логе.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyView()
    {
        IEnumerable<ProjectViewModel> query = _all;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            query = query.Where(vm =>
                vm.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || vm.Path.Contains(q, StringComparison.OrdinalIgnoreCase)
                || vm.TypeName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (vm.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (vm.GitHubUrl?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (FilterType != "Все")
        {
            query = FilterType == "Другие"
                ? query.Where(vm => vm.TypeName is not ("Android" or "Web" or ".NET"))
                : query.Where(vm => vm.TypeName == FilterType);
        }

        query = SortMode switch
        {
            "Имя" => query.OrderBy(vm => vm.Name, StringComparer.OrdinalIgnoreCase),
            "Статус" => query.OrderBy(vm => vm.StatusSortKey),
            "Git активность" => query.OrderByDescending(vm => vm.GitChangeCount),
            _ => query.OrderByDescending(vm => vm.LastActivity ?? DateTime.MinValue),
        };

        Projects.Clear();
        foreach (var vm in query)
        {
            Projects.Add(vm);
        }

        IsEmpty = Projects.Count == 0 && !IsBusy;

        var previousId = SelectedProject?.Entry.Id;
        SelectedProject = Projects.FirstOrDefault(p => p.Entry.Id == previousId) ?? Projects.FirstOrDefault();
    }

    [RelayCommand]
    private async Task AddProjectAsync()
    {
        var path = await FolderPicker.PickAsync();
        if (path is null)
        {
            return;
        }

        await AddProjectFromPathAsync(path);
    }

    public async Task AddProjectFromPathAsync(string path)
    {
        try
        {
            ErrorMessage = null;
            await _services.Projects.AddAsync(path);
        }
        catch (Exception ex)
        {
            _services.Logger.Error($"Add project failed: {path}", ex);
            ErrorMessage = ex.Message;
        }
    }
}
