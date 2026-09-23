using System.Runtime.InteropServices;
using QuadDesk.Core;
namespace QuadDesk.Native;

internal sealed record MonitorInfo(string DevicePath, string DeviceName, string FriendlyName, PixelRect Bounds, PixelRect WorkArea, bool Primary, uint RefreshRate)
{
    public override string ToString() => $"{DeviceName} · {FriendlyName} — {Bounds.Width}×{Bounds.Height} @ {RefreshRate} Hz · {(Primary ? "основной" : "дополнительный")} · ({Bounds.X}, {Bounds.Y})";
}
internal static class MonitorManager
{
    public static List<MonitorInfo> GetAll()
    {
        var result = new List<MonitorInfo>();
        foreach (var screen in Screen.AllScreens)
        {
            var device = new NativeMethods.DISPLAY_DEVICE { Size = (uint)Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
            // EDD_GET_DEVICE_INTERFACE_NAME returns the monitor device interface path, not DISPLAY1/2.
            if (!NativeMethods.EnumDisplayDevices(screen.DeviceName, 0, ref device, 1) || string.IsNullOrWhiteSpace(device.DeviceID)) continue;
            var mode = new NativeMethods.DEVMODE { Size = 220 };
            NativeMethods.EnumDisplaySettings(screen.DeviceName, -1, ref mode);
            static PixelRect R(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
            result.Add(new(device.DeviceID, screen.DeviceName, device.DeviceString, R(screen.Bounds), R(screen.WorkingArea), screen.Primary, mode.Frequency));
        }
        // Cloned displays with ambiguous identity must never be chosen automatically.
        return result.GroupBy(m => m.DevicePath, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() == 1).Select(g => g.Single()).ToList();
    }
    public static bool IsTarget(nint hwnd, MonitorInfo target)
    {
        var info = new NativeMethods.MONITORINFOEX { Size = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(), Device = "" };
        return NativeMethods.IsWindow(hwnd) && NativeMethods.GetMonitorInfo(NativeMethods.MonitorFromWindow(hwnd, 0), ref info) && string.Equals(info.Device, target.DeviceName, StringComparison.OrdinalIgnoreCase);
    }
}
