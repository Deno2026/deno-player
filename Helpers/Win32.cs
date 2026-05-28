using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DenoVideoPlayer.Helpers;

internal static class Win32
{
    // WindowFromVisual은 Visual의 HwndSource를 통한다.
    public static IntPtr GetHwnd(Window window)
        => new WindowInteropHelper(window).EnsureHandle();

    public static IntPtr GetHwndFromVisual(System.Windows.Media.Visual v)
        => (PresentationSource.FromVisual(v) as HwndSource)?.Handle ?? IntPtr.Zero;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    public static void MoveChildWindow(IntPtr child, int x, int y, int cx, int cy)
    {
        if (child == IntPtr.Zero) return;
        SetWindowPos(child, IntPtr.Zero, x, y, cx, cy, SWP_NOZORDER | SWP_NOACTIVATE);
    }
}
