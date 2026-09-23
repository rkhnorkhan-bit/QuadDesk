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
    public Func<string, bool>? InterceptCommand;
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
    readonly WinArrowInterceptor winArrow;
    public IReadOnlyList<HotkeyStatus> HotkeyStatuses => registry.Statuses;
    public EventHost()
    {
        ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None; Opacity = 0;
        registry = new(backend); backend.Window = Handle;
        winArrow = new(action => InterceptCommand?.Invoke(action) == true);
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
        if (disposing)
        {
            registry.Dispose();
            winArrow.Dispose();
        }
        base.Dispose(disposing);
    }
}
internal sealed class WinArrowInterceptor : IDisposable
{
    readonly NativeMethods.KeyboardEventProc callback;
    readonly Func<string, bool> handler;
    nint hook;
    bool disposed;
    public WinArrowInterceptor(Func<string, bool> handler)
    {
        this.handler = handler;
        callback = OnKeyboard;
        hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, callback, NativeMethods.GetModuleHandle(null), 0);
        if (hook == 0) Log.Write("Win+Arrow hook unavailable: " + Marshal.GetLastWin32Error());
    }
    nint OnKeyboard(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (wParam == NativeMethods.WmKeyDown || wParam == NativeMethods.WmSysKeyDown))
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            bool win = (NativeMethods.GetKeyState(NativeMethods.VkLWin) & unchecked((short)0x8000)) != 0 || (NativeMethods.GetKeyState(NativeMethods.VkRWin) & unchecked((short)0x8000)) != 0;
            if (win && CommandFor(data.VkCode) is { } command)
            {
                try { if (handler(command)) return 1; }
                catch (Exception ex) { Log.Write("Win+Arrow intercept failure: " + ex.GetType().Name); }
            }
        }
        return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
    }
    static string? CommandFor(uint vk) => vk switch
    {
        NativeMethods.VkLeft => "winsnap:left",
        NativeMethods.VkRight => "winsnap:right",
        NativeMethods.VkUp => "winsnap:up",
        NativeMethods.VkDown => "winsnap:down",
        _ => null
    };
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (hook != 0) NativeMethods.UnhookWindowsHookEx(hook);
        hook = 0;
        GC.KeepAlive(callback);
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
