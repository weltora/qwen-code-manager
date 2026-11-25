using System.Runtime.InteropServices;

namespace QwenCodeManager.App;

/// <summary>Win32 sizing helpers: WinUI 3 Window lacks MinWidth/MinHeight APIs.</summary>
internal static partial class WindowSize
{
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(IntPtr hwnd);

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GwlpWndProc = -4;
    private const uint WmGetMinMaxInfo = 0x0024;

    // Keep delegates alive for the process lifetime.
    private static readonly Dictionary<IntPtr, WndProc> Hooks = new();
    private static readonly Dictionary<IntPtr, IntPtr> OriginalProcs = new();
    private static readonly Dictionary<IntPtr, (int W, int H)> MinSizes = new();

    public static void SetMinSize(IntPtr hwnd, int width, int height)
    {
        MinSizes[hwnd] = (width, height);
        if (Hooks.ContainsKey(hwnd))
        {
            return;
        }

        WndProc hook = (h, msg, w, l) =>
        {
            if (msg == WmGetMinMaxInfo && MinSizes.TryGetValue(h, out var min))
            {
                var dpi = GetDpiForWindow(h);
                var scale = dpi == 0 ? 1.0 : dpi / 96.0;
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(l);
                mmi.ptMinTrackSize.x = (int)(min.W * scale);
                mmi.ptMinTrackSize.y = (int)(min.H * scale);
                Marshal.StructureToPtr(mmi, l, true);
            }

            return CallWindowProcW(OriginalProcs[h], h, msg, w, l);
        };

        Hooks[hwnd] = hook;
        var ptr = Marshal.GetFunctionPointerForDelegate(hook);
        OriginalProcs[hwnd] = SetWindowLongPtrW(hwnd, GwlpWndProc, ptr);
    }

    public static void Resize(IntPtr hwnd, int width, int height)
    {
        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, (int)(width * scale), (int)(height * scale), SWP_NOMOVE | SWP_NOZORDER);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }
}
