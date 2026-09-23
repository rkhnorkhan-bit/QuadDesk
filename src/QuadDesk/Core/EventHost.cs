using System.Runtime.InteropServices;
using QuadDesk.Core;
using QuadDesk.Native;
using QuadDesk.Services;
namespace QuadDesk;
internal sealed class EventHost : Form
{
    public event Action<string>? Command;
    public event Action? TopologyChanged;
    public event Action<bool>? LockChanged;
    sealed class Backend : IHotkeyBackend
    {
        public nint Window;
        public int Register(int id, HotkeyChord chord)
        {
            if (NativeMethods.RegisterHotKey(Window, id, chord.Modifiers | 0x4000, chord.Key)) return 0;
            int error = Marshal.GetLastWin32Error(); return error == 0 ? -1 : error;
        }
        public void Unregister(int id) => NativeMethods.UnregisterHotKey(Window, id);
    }
    readonly Backend backend = new();
    readonly HotkeyRegistry registry;
    public IReadOnlyList<HotkeyStatus> HotkeyStatuses => registry.Statuses;
    public EventHost()
    {
        ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None; Opacity = 0;
        registry = new(backend); backend.Window = Handle;
        if (!NativeMethods.WTSRegisterSessionNotification(Handle, 0)) Log.Write("Session notification unavailable; desktop guard remains active");
    }
    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);
    public IReadOnlyList<HotkeyStatus> Bind(Dictionary<string, string> hotkeys) => registry.Bind(hotkeys);
    public IReadOnlyList<HotkeyStatus> Probe(Dictionary<string, string> hotkeys) => registry.Probe(hotkeys);
    public void SuspendHotkeys() => registry.Clear();
    public void ResumeHotkeys() => registry.Rebind();
    public static bool TryParse(string value, out uint mods, out uint key)
    {
        bool valid = HotkeyChord.TryParse(value, out var chord);
        mods = chord.Modifiers; key = chord.Key; return valid;
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        registry?.Clear(); NativeMethods.WTSUnRegisterSessionNotification(backend.Window);
        base.OnHandleDestroyed(e);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e); backend.Window = Handle;
        if (registry is not null)
        {
            registry.Rebind(); NativeMethods.WTSRegisterSessionNotification(Handle, 0);
        }
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && registry.ActionFor(m.WParam.ToInt32()) is { } action) Command?.Invoke(action);
        if (m.Msg is 0x007E or 0x02E0 || m.Msg == 0x001A) TopologyChanged?.Invoke();
        if (m.Msg == 0x02B1 && m.WParam.ToInt32() is 7 or 8) LockChanged?.Invoke(m.WParam.ToInt32() == 7);
        base.WndProc(ref m);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) registry.Dispose();
        base.Dispose(disposing);
    }
}
internal sealed class WinEventHooks : IDisposable
{
    readonly NativeMethods.WinEventProc callback;
    readonly List<nint> hooks = [];
    public WinEventHooks(Action<uint, nint, uint> handler)
    {
        callback = (_, kind, hwnd, obj, child, _, time) =>
        {
            if (hwnd == 0 || obj != 0 || child != 0) return;
            try { handler(kind, hwnd, time); } catch (Exception ex) { Log.Write("WinEvent failure: " + ex.GetType().Name); }
        };
        try
        {
            foreach (var (a, b) in new (uint, uint)[] { (NativeMethods.MoveStart, NativeMethods.MoveEnd), (NativeMethods.Destroy, NativeMethods.Show), (NativeMethods.State, NativeMethods.Location), (NativeMethods.Foreground, NativeMethods.Foreground) })
            {
                var hook = NativeMethods.SetWinEventHook(a, b, 0, callback, 0, 0, 2); // OUTOFCONTEXT | SKIPOWNPROCESS
                if (hook == 0) throw new System.ComponentModel.Win32Exception();
                hooks.Add(hook);
            }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { foreach (var hook in hooks) NativeMethods.UnhookWinEvent(hook); hooks.Clear(); GC.KeepAlive(callback); }
}
