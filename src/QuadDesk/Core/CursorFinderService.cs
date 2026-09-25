using System.Runtime.InteropServices;
using QuadDesk.Core;
using QuadDesk.Native;
using QuadDesk.UI;

namespace QuadDesk;

internal sealed class CursorFinderService : IDisposable
{
    readonly record struct CursorSample(Point Position, long Tick);

    readonly QuadDeskController controller;
    readonly CursorFinderOverlayForm overlay = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 25 };
    readonly Queue<CursorSample> samples = new();

    long cooldownUntil;
    bool disposed;

    public CursorFinderService(QuadDeskController controller)
    {
        this.controller = controller;
        timer.Tick += (_, _) => Tick();
        controller.Changed += Sync;
        Sync();
    }

    public void Sync()
    {
        if (disposed)
            return;

        bool enabled = controller.Config.CursorFinderEnabled;
        timer.Enabled = enabled;

        if (!enabled)
        {
            samples.Clear();
            overlay.HideNow();
        }
    }

    void Tick()
    {
        if (!controller.Config.CursorFinderEnabled || !NativeMethods.IsDefaultDesktop())
            return;

        if (!NativeMethods.GetCursorPos(out var nativePoint))
            return;

        long now = Environment.TickCount64;
        var point = new Point(nativePoint.X, nativePoint.Y);
        samples.Enqueue(new(point, now));

        while (samples.Count > 0 && now - samples.Peek().Tick > 450)
            samples.Dequeue();

        if (now < cooldownUntil || samples.Count < 6)
            return;

        if (controller.Config.CursorFinderSuppressFullscreen && IsForegroundFullscreen())
            return;

        if (!LooksLikeShake())
            return;

        cooldownUntil = now + 900;
        overlay.Pulse(
            point,
            controller.Config.CursorFinderMaxSize,
            controller.Config.CursorFinderFadeMs,
            CursorFinderColor());
    }

    Color CursorFinderColor()
    {
        try
        {
            return ColorTranslator.FromHtml(controller.Config.CursorFinderColor);
        }
        catch
        {
            return SystemColors.Highlight;
        }
    }

    bool LooksLikeShake()
    {
        var points = samples.ToArray();
        if (points.Length < 6)
            return false;

        double total = 0;
        int flips = 0;
        int lastX = 0, lastY = 0;
        int directionalSteps = 0;

        for (int i = 1; i < points.Length; i++)
        {
            int dx = points[i].Position.X - points[i - 1].Position.X;
            int dy = points[i].Position.Y - points[i - 1].Position.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 4)
                continue;

            total += distance;

            int signX = Math.Abs(dx) >= 12 ? Math.Sign(dx) : 0;
            int signY = Math.Abs(dy) >= 12 ? Math.Sign(dy) : 0;

            if (signX != 0)
            {
                if (lastX != 0 && signX != lastX)
                    flips++;
                lastX = signX;
                directionalSteps++;
            }

            if (signY != 0)
            {
                if (lastY != 0 && signY != lastY)
                    flips++;
                lastY = signY;
                directionalSteps++;
            }
        }

        if (directionalSteps < 4)
            return false;

        var first = points[0].Position;
        var last = points[^1].Position;
        double net = Math.Sqrt(
            (last.X - first.X) * (last.X - first.X) +
            (last.Y - first.Y) * (last.Y - first.Y));

        return total >= controller.Config.CursorFinderSensitivity &&
               flips >= 3 &&
               net <= total * 0.65;
    }

    bool IsForegroundFullscreen()
    {
        nint hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
            return false;

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
            return true;

        nint monitor = NativeMethods.MonitorFromWindow(hwnd, 2);
        if (monitor == 0)
            return true;

        var info = new NativeMethods.MONITORINFOEX { Size = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            return true;

        PixelRect window = rect.Pixel;
        PixelRect screen = info.Monitor.Pixel;
        long overlap = window.Overlap(screen);
        bool coversScreen =
            Math.Abs(window.X - screen.X) <= 4 &&
            Math.Abs(window.Y - screen.Y) <= 4 &&
            Math.Abs(window.Width - screen.Width) <= 12 &&
            Math.Abs(window.Height - screen.Height) <= 12 &&
            overlap >= screen.Area * 0.90;

        return coversScreen;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        controller.Changed -= Sync;
        timer.Dispose();
        overlay.Dispose();
        samples.Clear();
    }
}
