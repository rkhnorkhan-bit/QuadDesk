using System.Runtime.InteropServices;
using QuadDesk.Services;
using Timer = System.Windows.Forms.Timer;

namespace QuadDesk;

internal sealed class WindowsSnapService : IDisposable
{
    const uint SpiGetWinArranging = 0x0082;
    const uint SpiSetWinArranging = 0x0083;
    const uint SpifUpdateIniFile = 0x0001;
    const uint SpifSendChange = 0x0002;

    readonly QuadDeskController controller;
    readonly Timer timer = new() { Interval = 1000 };
    bool? originalValue;
    bool applied;
    bool disposed;

    public WindowsSnapService(QuadDeskController controller)
    {
        this.controller = controller;
        timer.Tick += (_, _) => Sync();
        timer.Start();
        Sync();
    }

    bool Requested => controller.Active && controller.Config.SuspendWindowsSnap;
    public bool IsSuppressed => applied;

    public void Sync()
    {
        if (disposed)
            return;

        if (Requested)
            Suppress();
        else
            Restore();
    }

    void Suppress()
    {
        if (applied)
            return;

        if (!TryGet(out bool current))
        {
            Log.Write("Windows Snap suppression unavailable: cannot read SystemParametersInfo state.");
            return;
        }

        originalValue = current;

        if (current && !TrySet(false))
        {
            originalValue = null;
            Log.Write("Windows Snap suppression unavailable: cannot disable SystemParametersInfo state.");
            return;
        }

        applied = true;
        Log.Write("Windows Snap suppressed while QuadDesk is active.");
    }

    void Restore()
    {
        if (!applied)
            return;

        if (originalValue is { } value && !TrySet(value))
            Log.Write("Windows Snap restore failed; user can restore it in Windows Settings > System > Multitasking.");
        else
            Log.Write("Windows Snap restored to previous state.");

        applied = false;
        originalValue = null;
    }

    static bool TryGet(out bool enabled)
    {
        enabled = false;
        bool value = false;
        bool ok = SystemParametersInfo(SpiGetWinArranging, 0, ref value, 0);
        if (ok) enabled = value;
        return ok;
    }

    static bool TrySet(bool enabled)
    {
        bool value = enabled;
        return SystemParametersInfo(SpiSetWinArranging, 0, ref value, SpifUpdateIniFile | SpifSendChange);
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SystemParametersInfo(uint action, uint param, ref bool value, uint flags);

    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        Restore();
        timer.Dispose();
    }
}
