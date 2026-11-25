namespace QwenCodeManager.App.Services;

/// <summary>Simple cross-page navigation hook owned by MainWindow.</summary>
public static class NavigationService
{
    public static event Action<string>? NavigateRequested;

    public static void Navigate(string tag) => NavigateRequested?.Invoke(tag);
}
