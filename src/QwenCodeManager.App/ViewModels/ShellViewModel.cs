using CommunityToolkit.Mvvm.ComponentModel;
using QwenCodeManager.App.Services;

namespace QwenCodeManager.App.ViewModels;

/// <summary>Shell-level status shown in the bottom bar and sidebar Qwen card.</summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty]
    private string _qwenStatusText = "Определение...";

    [ObservableProperty]
    private bool _qwenFound;

    [ObservableProperty]
    private string _qwenVersionText = string.Empty;

    [ObservableProperty]
    private int _totalSessions;

    [ObservableProperty]
    private string _memoryStatusText = "неизвестно";

    [ObservableProperty]
    private bool _memoryAvailable;

    public ShellViewModel(AppServices services) => _services = services;

    public async Task RefreshAsync()
    {
        QwenFound = _services.QwenInfo.Found;
        QwenStatusText = QwenFound ? "найден" : "не найден";
        QwenVersionText = QwenFound ? $"v{_services.QwenInfo.Version}" : string.Empty;

        try
        {
            var qwenMd = Path.Combine(_services.Paths.QwenHome, "QWEN.md");
            MemoryAvailable = File.Exists(qwenMd);
            MemoryStatusText = MemoryAvailable ? "активна" : "не найдена";

            var total = await Task.Run(() =>
                _services.Projects.Projects.Sum(p => _services.Sessions.GetSummary(p.Path).Total));
            TotalSessions = total;
        }
        catch (Exception ex)
        {
            _services.Logger.Warn($"Shell status refresh failed: {ex.Message}");
        }
    }
}
