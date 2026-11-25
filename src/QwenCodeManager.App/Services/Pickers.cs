using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;

namespace QwenCodeManager.App.Services;

/// <summary>WinUI pickers must be initialized with the window handle in unpackaged apps.</summary>
public static class FolderPicker
{
    public static async Task<string?> PickAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch
        {
            return null;
        }
    }
}

public static class FilePicker
{
    public static async Task<string?> PickAsync(IEnumerable<string> extensions)
    {
        try
        {
            var picker = new FileOpenPicker();
            foreach (var ext in extensions)
            {
                picker.FileTypeFilter.Add(ext);
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch
        {
            return null;
        }
    }
}
