using Microsoft.UI.Xaml;
using QwenCodeManager.App.Services;

namespace QwenCodeManager.App;

public partial class App : Application
{
    private Window? _window;

    public static AppServices Services { get; private set; } = null!;
    public static Window MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Services?.Logger.Error("Unhandled UI exception", e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = AppServices.Create();
        Services.Logger.Info("Qwen Code Manager started.");

        var window = new MainWindow();
        _window = window;
        MainWindow = window;
        window.Activate();
    }
}
