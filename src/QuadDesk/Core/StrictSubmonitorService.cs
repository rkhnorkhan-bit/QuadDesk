using QuadDesk.Core;
using QuadDesk.Native;
using QuadDesk.Services;
using Timer = System.Windows.Forms.Timer;

namespace QuadDesk;

internal sealed class StrictSubmonitorService : IDisposable
{
    sealed class ManagedWindow
    {
        public uint ProcessId;
        public string ZoneId = "";
        public PixelRect LastValidRect;
        public LocalSnapSlot Slot;
        public uint SuppressUntil;
    }

    readonly QuadDeskController controller;
    readonly Timer timer = new();
    readonly Dictionary<nint, ManagedWindow> managed = [];
    bool disposed, mutating;

    public StrictSubmonitorService(QuadDeskController controller)
    {
        this.controller = controller;
        timer.Interval = Math.Clamp(controller.Config.GuardIntervalMs, 50, 1000);
        timer.Tick += (_, _) => Guard();
        timer.Start();
    }

    bool Enabled => controller.Active && controller.Config.StrictSubmonitors && controller.Target is not null;

    public bool HandleWinCommand(string command)
    {
        if (disposed || !controller.Active || !controller.Config.CaptureWinArrow || controller.Target is null)
            return false;

        var hwnd = NativeMethods.GetForegroundWindow();
        if (!CanManage(hwnd))
            return false;

        var zone = ResolveZone(hwnd);
        if (zone is null)
            return false;

        var slot = command switch
        {
            "winsnap:left" => LocalSnapSlot.Left,
            "winsnap:right" => LocalSnapSlot.Right,
            "winsnap:up" => LocalSnapSlot.Full,
            "winsnap:down" => LocalSnapSlot.Free,
            _ => LocalSnapSlot.Free
        };

        if (command == "winsnap:down")
        {
            RestoreFree(hwnd, zone);
            return true;
        }

        if (slot == LocalSnapSlot.Free)
            return false;

        Place(hwnd, zone, LayoutEngine.LocalSnapRect(zone.Bounds, slot), slot);
        return true;
    }

    void Guard()
    {
        if (disposed)
            return;

        int interval = Math.Clamp(controller.Config.GuardIntervalMs, 50, 1000);
        if (timer.Interval != interval)
            timer.Interval = interval;

        foreach (var hwnd in managed.Keys.Where(h => !NativeMethods.IsWindow(h)).ToArray())
            managed.Remove(hwnd);

        if (!Enabled || mutating)
            return;

        foreach (var hwnd in WindowManager.Enumerate())
        {
            if (!CanManage(hwnd))
                continue;

            var zone = ResolveZone(hwnd);
            if (zone is null)
                continue;

            var state = Track(hwnd);
            var rect = WindowManager.Bounds(hwnd);
            if (rect is null)
                continue;

            if (NativeMethods.IsZoomed(hwnd))
            {
                Place(hwnd, zone, zone.Bounds, LocalSnapSlot.Full);
                continue;
            }

            if (state.SuppressUntil != 0 && unchecked((int)(Environment.TickCount - state.SuppressUntil)) <= 0)
                continue;

            if (!LayoutEngine.IsInside(rect.Value, zone.Bounds))
            {
                var fitted = rect.Value.FitInside(zone.Bounds);
                if (fitted != rect.Value)
                {
                    Place(hwnd, zone, fitted, LocalSnapSlot.Free);
                    Log.Write($"Strict clamp: hwnd={hwnd}; zone={zone.Id}; rect={rect.Value}; fitted={fitted}");
                    continue;
                }
            }

            state.ZoneId = zone.Id;
            state.LastValidRect = rect.Value.FitInside(zone.Bounds);
            state.Slot = LayoutEngine.MatchSnapRect(zone.Bounds, rect.Value);
        }
    }

    bool CanManage(nint hwnd) =>
        controller.Active &&
        controller.Target is { } target &&
        NativeMethods.IsDefaultDesktop() &&
        MonitorManager.IsTarget(hwnd, target) &&
        WindowManager.Eligible(hwnd, controller.Config);

    ManagedWindow Track(nint hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (!managed.TryGetValue(hwnd, out var state) || state.ProcessId != pid)
        {
            var rect = WindowManager.Bounds(hwnd) ?? default;
            var zone = LayoutEngine.Detect(controller.Zones, rect);
            state = new ManagedWindow
            {
                ProcessId = pid,
                ZoneId = zone?.Id ?? "",
                LastValidRect = zone is null ? rect : rect.FitInside(zone.Bounds),
                Slot = zone is null ? LocalSnapSlot.Free : LayoutEngine.MatchSnapRect(zone.Bounds, rect)
            };
            managed[hwnd] = state;
        }
        return state;
    }

    Zone? ResolveZone(nint hwnd)
    {
        var state = Track(hwnd);
        var zone = controller.Zones.FirstOrDefault(z => z.Id == state.ZoneId);
        if (zone is not null)
            return zone;

        var rect = WindowManager.Bounds(hwnd);
        if (rect is null)
            return null;

        zone = LayoutEngine.Detect(controller.Zones, rect.Value);
        if (zone is not null)
        {
            state.ZoneId = zone.Id;
            state.LastValidRect = rect.Value.FitInside(zone.Bounds);
            return zone;
        }

        var center = new PixelRect(rect.Value.X + rect.Value.Width / 2, rect.Value.Y + rect.Value.Height / 2, 1, 1);
        zone = controller.Zones.FirstOrDefault(z => z.Bounds.Contains(center.X, center.Y));
        if (zone is not null)
        {
            state.ZoneId = zone.Id;
            state.LastValidRect = rect.Value.FitInside(zone.Bounds);
        }
        return zone;
    }

    void RestoreFree(nint hwnd, Zone zone)
    {
        var state = Track(hwnd);
        var rect = state.LastValidRect.Width > 0 && state.LastValidRect.Height > 0
            ? state.LastValidRect.FitInside(zone.Bounds)
            : new PixelRect(zone.Bounds.X + zone.Bounds.Width / 10, zone.Bounds.Y + zone.Bounds.Height / 10, Math.Max(1, zone.Bounds.Width * 8 / 10), Math.Max(1, zone.Bounds.Height * 8 / 10));
        Place(hwnd, zone, rect, LocalSnapSlot.Free);
    }

    bool Place(nint hwnd, Zone zone, PixelRect target, LocalSnapSlot slot)
    {
        if (!CanManage(hwnd))
            return false;

        mutating = true;
        try
        {
            bool ok = WindowManager.Place(hwnd, target.FitInside(zone.Bounds));
            var state = Track(hwnd);
            state.ZoneId = zone.Id;
            state.Slot = slot;
            state.SuppressUntil = unchecked((uint)Environment.TickCount + 125u);
            if (WindowManager.Bounds(hwnd) is { } placed)
                state.LastValidRect = placed.FitInside(zone.Bounds);
            return ok;
        }
        finally
        {
            mutating = false;
        }
    }

    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        timer.Dispose();
        managed.Clear();
    }
}
