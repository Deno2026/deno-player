using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DenoPlayer.Helpers;

/// <summary>
/// WPF 안에 child Win32 window 한 개 만들고 그 HWND를 mpv --wid 로 넘기는 컨테이너.
/// HwndHost를 직접 구현해서 WindowsForms 의존성 없이 가벼움.
///
/// keyboard focus: mpv는 attach된 후 그 안에 자기 child hwnd를 만들고 사용자 클릭 시
/// 거기로 SetFocus한다. 그러면 WPF window가 keyboard focus를 잃어 KeyBinding(F/V/ESC 등)이
/// 작동 안 한다. 우리 host hwnd의 WndProc에서 mouse 이벤트를 가로채 즉시 owner WPF
/// window로 focus를 돌려준다. (mpv는 마우스 wheel/cursor 정보는 IPC mouse-pos로 따로 받음.)
/// </summary>
public sealed class Win32VideoHost : HwndHost
{
    private const int WS_CHILD     = 0x40000000;
    private const int WS_VISIBLE   = 0x10000000;
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const int WS_CLIPSIBLINGS = 0x04000000;
    private const int WM_SETFOCUS    = 0x0007;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_KEYUP       = 0x0101;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int WM_SYSKEYUP    = 0x0105;
    private const int MA_NOACTIVATEANDEAT = 4;
    private const int MA_NOACTIVATE       = 3;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        int dwExStyle, string lpClassName, string? lpWindowName,
        int dwStyle, int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    private const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

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

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // host hwnd는 click으로 activate 안 함 → owner WPF window가 keyboard focus를 잃지 않음.
        // (검증: 영상 위 click 후에도 WPF KeyBinding이 정상 fire함을 확인.)
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (hwnd.Handle != IntPtr.Zero) DestroyWindow(hwnd.Handle);
        Hwnd = IntPtr.Zero;
    }
}
