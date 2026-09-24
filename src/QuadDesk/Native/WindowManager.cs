using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using QuadDesk.Core;
using QuadDesk.Services;
namespace QuadDesk.Native;

internal static class WindowManager
{
    static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase) { "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "#32768", "#32770", "tooltips_class32", "IME", "MSCTFIME UI" };
    static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase) { "StartMenuExperienceHost", "SearchHost", "ShellExperienceHost", "LockApp", "TextInputHost" };
    static readonly HashSet<string> TerminalProcesses = new(StringComparer.OrdinalIgnoreCase) { "WindowsTerminal", "wt", "OpenConsole", "conhost", "cmd", "powershell", "pwsh" };
    public static string ClassName(nint hwnd) { var s = new StringBuilder(256); NativeMethods.GetClassName(hwnd, s, s.Capacity); return s.ToString(); }
    public static string Title(nint hwnd) { var s = new StringBuilder(512); NativeMethods.GetWindowText(hwnd, s, s.Capacity); return s.ToString(); }
    public static PixelRect? Bounds(nint hwnd)
    {
        if (!NativeMethods.IsWindow(hwnd)) return null;
        if (NativeMethods.DwmRect(hwnd, 9, out var frame, Marshal.SizeOf<NativeMethods.RECT>()) == 0) return frame.Pixel;
        return NativeMethods.GetWindowRect(hwnd, out var rect) ? rect.Pixel : null;
    }
    public static WindowIdentity? Identity(nint hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var p = Process.GetProcessById(checked((int)pid));
            string path; try { path = p.MainModule?.FileName ?? ""; } catch { path = ""; }
            return new(p.ProcessName, path, ClassName(hwnd));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or OverflowException) { return null; }
    }
    public static bool Eligible(nint hwnd, AppConfig config, bool allowMinimized = false)
    {
        if (!NativeMethods.IsWindow(hwnd) || NativeMethods.IsHungAppWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd) || (!allowMinimized && NativeMethods.IsIconic(hwnd)) || NativeMethods.GetAncestor(hwnd, 2) != hwnd || NativeMethods.GetWindow(hwnd, 4) != 0) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId) return false;
        long style = NativeMethods.GetWindowLongPtr(hwnd, -16).ToInt64(), ex = NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64();
        var className = ClassName(hwnd);
        var identity = Identity(hwnd);
        if (identity is null) return false;
        bool terminalLike = TerminalProcesses.Contains(identity.ProcessName) || className.Contains("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase) || className.Contains("CASCADIA", StringComparison.OrdinalIgnoreCase);
        if ((ex & (0x80 | 0x08000000)) != 0 || (style & 0x40000000) != 0 || (style & 0x00C00000) == 0 || (!terminalLike && (style & 0x00040000) == 0)) return false;
        if (ShellClasses.Contains(className)) return false;
        if (NativeMethods.DwmInt(hwnd, 14, out int cloaked, 4) != 0 || cloaked != 0) return false;
        var bounds = Bounds(hwnd); if (bounds is null || bounds.Value.Width < 80 || bounds.Value.Height < 60) return false;
        return !ShellProcesses.Contains(identity.ProcessName) && !config.ExcludedProcesses.Any(p => string.Equals(Path.GetFileNameWithoutExtension(p), identity.ProcessName, StringComparison.OrdinalIgnoreCase));
    }
    public static bool Place(nint hwnd, PixelRect target)
    {
        if (!NativeMethods.IsWindow(hwnd) || target.Width <= 0 || target.Height <= 0) return false;
        if (NativeMethods.IsZoomed(hwnd)) NativeMethods.ShowWindow(hwnd, 9);
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.GetWindowRect(hwnd, out var outer)) return false;
        var originalOuter = outer;
        var visible = Bounds(hwnd) ?? outer.Pixel;
        int l = visible.X - outer.Left, t = visible.Y - outer.Top, r = outer.Right - visible.Right, b = outer.Bottom - visible.Bottom;
        bool ok = NativeMethods.SetWindowPos(hwnd, 0, target.X - l, target.Y - t, target.Width + l + r, target.Height + t + b, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
        if (!ok) { Log.Write($"SetWindowPos error={Marshal.GetLastWin32Error()}"); return false; }
        // A single correction handles new invisible border metrics after a mixed-DPI move.
        if (NativeMethods.IsWindow(hwnd) && NativeMethods.GetWindowRect(hwnd, out outer) && Bounds(hwnd) is { } actual && actual != target)
        {
            l = actual.X - outer.Left; t = actual.Y - outer.Top; r = outer.Right - actual.Right; b = outer.Bottom - actual.Bottom;
            ok = NativeMethods.SetWindowPos(hwnd, 0, target.X - l, target.Y - t, target.Width + l + r, target.Height + t + b, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
            if (!ok) Log.Write($"SetWindowPos correction error={Marshal.GetLastWin32Error()}");
        }
        if (ok && Bounds(hwnd) is { } final && (Math.Abs(final.X - target.X) > 3 || Math.Abs(final.Y - target.Y) > 3 || Math.Abs(final.Width - target.Width) > 3 || Math.Abs(final.Height - target.Height) > 3))
        {
            if (NativeMethods.IsWindow(hwnd)) NativeMethods.SetWindowPos(hwnd, 0, originalOuter.Left, originalOuter.Top, originalOuter.Right - originalOuter.Left, originalOuter.Bottom - originalOuter.Top, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
            Log.Write("Window refused zone geometry; reverted placement"); return false;
        }
        return ok;
    }
    public static List<nint> Enumerate()
    {
        var windows = new List<nint>(); NativeMethods.EnumWindows((hwnd, _) => { windows.Add(hwnd); return true; }, 0); return windows;
    }
}
