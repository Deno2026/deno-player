using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DenoPlayer.Helpers;

/// <summary>
/// WPF 안에 child Win32 window 한 개 만들고 그 HWND를 mpv --wid 로 넘기는 컨테이너.
/// HwndHost를 직접 구현해서 WindowsForms 의존성 없이 가벼움.
/// </summary>
public sealed class Win32VideoHost : HwndHost
{
    private const int WS_CHILD     = 0x40000000;
    private const int WS_VISIBLE   = 0x10000000;
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const int WS_CLIPSIBLINGS = 0x04000000;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        int dwExStyle, string lpClassName, string? lpWindowName,
        int dwStyle, int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    public IntPtr Hwnd { get; private set; }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // mpv는 child window를 stretch 채움. 클래스는 OS 표준 "Static" 사용(작성 부담 없음).
        var h = CreateWindowExW(
            0,
            "Static",
            null,
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 16, 16,
            hwndParent.Handle,
            IntPtr.Zero,
            GetModuleHandleW(null),
            IntPtr.Zero);
        Hwnd = h;
        return new HandleRef(this, h);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (hwnd.Handle != IntPtr.Zero) DestroyWindow(hwnd.Handle);
        Hwnd = IntPtr.Zero;
    }
}
