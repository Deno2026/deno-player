using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DenoVideoPlayer.Helpers;

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
    private const int WM_PAINT = 0x000F;
    private const int WM_SIZE = 0x0005;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_SETFOCUS    = 0x0007;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL  = 0x020A;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_KEYUP       = 0x0101;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int WM_SYSKEYUP    = 0x0105;
    private const int MK_MBUTTON = 0x0010;
    private const int MA_ACTIVATE         = 1;
    private const int MA_NOACTIVATEANDEAT = 4;
    private const int MA_NOACTIVATE       = 3;
    private const int GWLP_WNDPROC = -4;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOCOPYBITS = 0x0100;
    private const uint SWP_NOOWNERZORDER = 0x0200;
    private const uint SWP_DEFERERASE = 0x2000;
    private const int VideoSurfaceBackgroundColorRef = 0x00050705;

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
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(
        IntPtr lpPrevWndFunc,
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, out NativePaintStruct lpPaint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndPaint(IntPtr hWnd, ref NativePaintStruct lpPaint);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hDC, ref NativeRect lprc, IntPtr hbr);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(int colorRef);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    private delegate IntPtr NativeWndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePaintStruct
    {
        public IntPtr hdc;
        public int fErase;
        public NativeRect rcPaint;
        public int fRestore;
        public int fIncUpdate;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    private IntPtr _outerHwnd;
    private IntPtr _oldInnerWndProc;
    private NativeWndProc? _innerWndProc;
    private int _lastTransformX = int.MinValue;
    private int _lastTransformY = int.MinValue;
    private int _lastTransformWidth = int.MinValue;
    private int _lastTransformHeight = int.MinValue;

    public IntPtr Hwnd { get; private set; }
    public event Action? DoubleClicked;
    public event Action? ActivationRequested;
    public event Action? MiddleButtonDown;
    public event Action? MiddleButtonMove;
    public event Action? MiddleButtonUp;
    public event Action<int>? MouseWheelDelta;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // Outer hwnd는 WPF가 배치하는 고정 clip 역할, Inner hwnd는 mpv가 실제로 그리는 표면.
        var outer = CreateWindowExW(
            0,
            "Static",
            null,
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 16, 16,
            hwndParent.Handle,
            IntPtr.Zero,
            GetModuleHandleW(null),
            IntPtr.Zero);
        _outerHwnd = outer;

        var inner = CreateWindowExW(
            0,
            "Static",
            null,
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 16, 16,
            outer,
            IntPtr.Zero,
            GetModuleHandleW(null),
            IntPtr.Zero);
        Hwnd = inner;
        _innerWndProc = InnerWndProc;
        _oldInnerWndProc = SetWindowLongPtr(inner, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_innerWndProc));
        FitVideoViewportToHost();
        return new HandleRef(this, outer);
    }

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var result = HandleVideoWindowMessage(hwnd, msg, wParam, lParam, ref handled);
        if (handled)
            return result;
        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    private IntPtr InnerWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        var handled = false;
        var result = HandleVideoWindowMessage(hwnd, msg, wParam, lParam, ref handled);
        if (handled)
            return result;

        return _oldInnerWndProc != IntPtr.Zero
            ? CallWindowProc(_oldInnerWndProc, hwnd, msg, wParam, lParam)
            : IntPtr.Zero;
    }

    private IntPtr HandleVideoWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_ERASEBKGND)
        {
            handled = true;
            FillNativeBackground(hwnd, wParam);
            return new IntPtr(1);
        }
        if (msg == WM_PAINT && hwnd == _outerHwnd)
        {
            handled = true;
            var hdc = BeginPaint(hwnd, out var paint);
            FillNativeBackground(hwnd, hdc);
            EndPaint(hwnd, ref paint);
            return IntPtr.Zero;
        }
        if (msg == WM_SIZE && hwnd == _outerHwnd)
        {
            FitVideoViewportToHost();
        }

        // 비활성 창의 첫 click은 owner를 정상 활성화한다. 이미 활성화된 상태에서는
        // native child가 WPF keyboard focus를 빼앗지 않도록 MA_NOACTIVATE를 유지한다.
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            var root = GetAncestor(hwnd, GA_ROOT);
            if (root != IntPtr.Zero && root != GetForegroundWindow())
            {
                ActivationRequested?.Invoke();
                return new IntPtr(MA_ACTIVATE);
            }
            return new IntPtr(MA_NOACTIVATE);
        }
        if (msg == WM_LBUTTONDBLCLK)
        {
            handled = true;
            DoubleClicked?.Invoke();
            return IntPtr.Zero;
        }
        if (msg == WM_MBUTTONDOWN)
        {
            handled = true;
            MiddleButtonDown?.Invoke();
            return IntPtr.Zero;
        }
        if (msg == WM_MBUTTONUP)
        {
            handled = true;
            MiddleButtonUp?.Invoke();
            return IntPtr.Zero;
        }
        if (msg == WM_MOUSEMOVE && (wParam.ToInt64() & MK_MBUTTON) != 0)
        {
            handled = true;
            MiddleButtonMove?.Invoke();
            return IntPtr.Zero;
        }
        if (msg == WM_MOUSEWHEEL)
        {
            handled = true;
            var delta = (short)((wParam.ToInt64() >> 16) & 0xffff);
            MouseWheelDelta?.Invoke(delta);
            return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    public void FitVideoViewportToHost()
    {
        if (_outerHwnd == IntPtr.Zero || Hwnd == IntPtr.Zero)
            return;
        if (!GetClientRect(_outerHwnd, out var rect))
            return;

        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);

        if (_lastTransformX == 0 &&
            _lastTransformY == 0 &&
            width == _lastTransformWidth &&
            height == _lastTransformHeight)
            return;

        _lastTransformX = 0;
        _lastTransformY = 0;
        _lastTransformWidth = width;
        _lastTransformHeight = height;

        SetWindowPos(Hwnd, IntPtr.Zero, 0, 0, width, height,
            SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_NOACTIVATE | SWP_NOCOPYBITS | SWP_DEFERERASE);
    }

    private static void FillNativeBackground(IntPtr hwnd, IntPtr hdc)
    {
        if (hwnd == IntPtr.Zero || hdc == IntPtr.Zero)
            return;
        if (!GetClientRect(hwnd, out var rect))
            return;

        var brush = CreateSolidBrush(VideoSurfaceBackgroundColorRef);
        if (brush == IntPtr.Zero)
            return;

        try
        {
            FillRect(hdc, ref rect, brush);
        }
        finally
        {
            DeleteObject(brush);
        }
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (Hwnd != IntPtr.Zero && _oldInnerWndProc != IntPtr.Zero)
            SetWindowLongPtr(Hwnd, GWLP_WNDPROC, _oldInnerWndProc);
        if (hwnd.Handle != IntPtr.Zero) DestroyWindow(hwnd.Handle);
        _outerHwnd = IntPtr.Zero;
        Hwnd = IntPtr.Zero;
        _oldInnerWndProc = IntPtr.Zero;
        _innerWndProc = null;
        _lastTransformX = _lastTransformY = int.MinValue;
        _lastTransformWidth = _lastTransformHeight = int.MinValue;
    }
}
