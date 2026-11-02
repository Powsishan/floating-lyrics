using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace FloatingLyrics.Platform.Overlay;

public static class OverlayInterop
{
    private const int ExStyle = -20;
    private const long Transparent = 0x20, ToolWindow = 0x80, AppWindow = 0x40000, NoActivate = 0x08000000;
    private const uint NoSize = 0x1, NoMove = 0x2, NoActivation = 0x10, FrameChanged = 0x20;

    public static void Configure(Window window, bool locked, bool topmost)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        var styles = GetStyle(handle).ToInt64();
        styles = (styles | ToolWindow | NoActivate) & ~AppWindow;
        styles = locked ? styles | Transparent : styles & ~Transparent;
        SetStyle(handle, new IntPtr(styles));
        SetWindowPos(handle, topmost ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, NoMove | NoSize | NoActivation | FrameChanged);
    }

    // Persistence is in physical pixels; WPF width remains in DIPs. Never scale a virtual-screen origin as if all monitors shared a DPI.
    public static (double X, double Y) GetPosition(Window window)
    {
        GetWindowRect(new WindowInteropHelper(window).Handle, out var rectangle);
        return (rectangle.Left, rectangle.Top);
    }

    public static void Place(Window window, double? left, double? top, bool bottomCenter = false)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        GetWindowRect(handle, out var rect);
        var area = Forms.Screen.FromHandle(handle).WorkingArea;
        var dpi = GetDpiForWindow(handle) / 96d;
        var width = Math.Min((int)(window.Width * dpi), area.Width);
        var height = Math.Min((int)(window.Height * dpi), area.Height);
        var x = bottomCenter || left is null ? area.Left + (area.Width - width) / 2 : (int)left.Value;
        var y = bottomCenter || top is null ? area.Bottom - height - (int)(48 * dpi) : (int)top.Value;
        var target = Forms.Screen.AllScreens.FirstOrDefault(screen => screen.WorkingArea.IntersectsWith(new System.Drawing.Rectangle(x, y, Math.Max(1, width), Math.Max(1, height))));
        area = target?.WorkingArea ?? Forms.Screen.PrimaryScreen!.WorkingArea;
        width = Math.Min(width, area.Width);
        height = Math.Min(height, area.Height);
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - height));
        SetWindowPos(handle, IntPtr.Zero, x, y, width, height, NoActivation | 0x4);
    }

    private static IntPtr GetStyle(IntPtr h) => IntPtr.Size == 8 ? GetWindowLongPtr(h, ExStyle) : new IntPtr(GetWindowLong(h, ExStyle));
    private static void SetStyle(IntPtr h, IntPtr style)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr(h, ExStyle, style); else SetWindowLong(h, ExStyle, style.ToInt32());
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr h, int index, int value);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int width, int height, uint flags);
}
