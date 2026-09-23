using QuadDesk.Core;
using QuadDesk.Native;
using QuadDesk.Services;
using QuadDesk.UI;
namespace QuadDesk;

internal sealed class WindowRuntimeState
{
    public uint ProcessId;
    public string ZoneId = "";
    public string LayoutId = "";
    public PixelRect PreviousRect;
    public PixelRect LastRect;
    public bool LogicalMaximized;
    public bool ManualOverride;
    public bool InitialRuleChecked;
    public uint SuppressThrough;
}
internal sealed class QuadDeskController : IDisposable
{
    public Storage Store { get; }
    public AppConfig Config { get; private set; }
    public List<LayoutDefinition> Layouts { get; }
    public LayoutDefinition Layout => Layouts.First(l => l.Id == Config.ActiveLayoutId);
    public MonitorInfo? Target { get; private set; }
    public List<Zone> Zones { get; private set; } = [];
    public string Status { get; private set; } = "Приостановлено";
    public bool Active => Config.Enabled && Target is not null && Zones.Count > 0 && !locked;
    public event Action? Changed;
    public event Action<string>? Notice;
    readonly EventHost host;
    readonly WinEventHooks hooks;
    readonly ZoneOverlayForm overlay = new();
    readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 5000 };
    readonly Dictionary<nint, WindowRuntimeState> states = [];
    bool locked, mutating, disposed;
    nint dragging;
    PixelRect dragStartRect;
    bool dragResized;
    public nint LastForeground { get; private set; }
    public IReadOnlyList<HotkeyStatus> HotkeyStatuses => host.HotkeyStatuses;
    public IReadOnlyList<HotkeyStatus> CheckHotkeys(Dictionary<string, string> keys) => host.Probe(keys);
    public void SuspendHotkeys() => host.SuspendHotkeys();
    public void ResumeHotkeys() => host.ResumeHotkeys();
    public PixelRect TargetBounds => Target is null ? new(0, 0, 3840, 2160) : Config.UseFullMonitorBounds ? Target.Bounds : Target.WorkArea;
    public QuadDeskController(Storage storage, EventHost host)
    {
        Store = storage; this.host = host; Config = storage.LoadConfig(); Layouts = storage.LoadLayouts();
        if (!Layouts.Any(l => l.Id == Config.ActiveLayoutId)) Config.ActiveLayoutId = Layouts[0].Id;
        host.Command += Execute; host.TopologyChanged += RefreshMonitor;
        host.LockChanged += OnLock;
        previewTimer.Tick += (_, _) => { previewTimer.Stop(); overlay.Hide(); };
        hooks = new WinEventHooks(OnEvent); RefreshMonitor();
    }
    void OnLock(bool value) { locked = value; dragging = 0; overlay.Hide(); RefreshMonitor(); }
    public void Start()
    {
        LastForeground = NativeMethods.GetForegroundWindow();
        BindHotkeys();
        if (Store.Warnings.Count > 0) Notice?.Invoke(string.Join("\n", Store.Warnings));
        if (Config.RestoreWorkspaceOnStart) TryRestoreWorkspace();
        Scan();
    }
    public void Save() => Storage.Save(Store.ConfigPath, Config);
    public void ApplySettings(AppConfig c)
    {
        Storage.ValidateConfig(c);
        if (!Layouts.Any(l => l.Id == c.ActiveLayoutId)) throw new ArgumentException("Нет такой раскладки.");
        Storage.Save(Store.ConfigPath, c); Config = c; dragging = 0; overlay.Hide();
        BindHotkeys();
        RefreshMonitor();
    }
    void BindHotkeys()
    {
        var results = host.Bind(Config.Hotkeys);
        var failed = results.Where(s => !s.Registered && !string.IsNullOrWhiteSpace(s.Binding)).ToList();
        foreach (var s in failed) Log.Write($"Hotkey {s.Action}: {s.Binding}; {s.Message}");
        if (failed.Count > 0) Notice?.Invoke($"Не работают сочетания: {failed.Count}. Откройте «Горячие клавиши» → «Базовый набор» → «Проверить» → «Сохранить». Управление мышью доступно.");
        Changed?.Invoke();
    }
    public void ApplyHotkeys(Dictionary<string, string> hotkeys)
    { var c = JsonData.Clone(Config); c.Hotkeys = hotkeys; ApplySettings(c); }
    public void PreviewZones()
    {
        if (Target is null || Zones.Count == 0) throw new InvalidOperationException("Сначала выберите подключённый дисплей и раскладку.");
        if (locked || !NativeMethods.IsDefaultDesktop()) return;
        dragging = 0; previewTimer.Stop();
        overlay.Display(TargetBounds, Zones, null); previewTimer.Start();
    }
    public void ArrangeWindows()
    {
        if (!Active) throw new InvalidOperationException("Сначала нажмите «Включить зоны» и выберите дисплей.");
        var windows = WindowManager.Enumerate().Where(CanTouch).ToList();
        int placed = 0;
        for (int i = 0; i < windows.Count; i++) if (Snap(windows[i], Zones[i % Zones.Count])) placed++;
        Notice?.Invoke(windows.Count == 0 ? "На выбранном дисплее нет подходящих окон. Перенесите туда Блокнот или браузер и повторите." : $"Размещено окон: {placed} из {windows.Count}. Если окон больше зон, некоторые зоны содержат несколько окон.");
    }
    public void RefreshMonitor()
    {
        previewTimer.Stop(); overlay.Hide(); dragging = 0;
        Target = MonitorManager.GetAll().SingleOrDefault(m => string.Equals(m.DevicePath, Config.TargetDevicePath, StringComparison.OrdinalIgnoreCase));
        Zones = [];
        if (Target is not null)
        {
            try { Zones = LayoutEngine.Calculate(Layout, TargetBounds, Config.MinimumZoneWidth, Config.MinimumZoneHeight); }
            catch (ArgumentException) { Status = "Приостановлено: зоны меньше минимального размера"; Changed?.Invoke(); return; }
        }
        Status = locked ? "Приостановлено: сеанс заблокирован" : Target is null ? "Приостановлено: выберите подключённый дисплей" : Config.Enabled ? "Активно" : "Выключено";
        // No automatic moves on topology changes: Windows may have migrated windows to another display.
        Changed?.Invoke(); Log.Write("Monitor topology refreshed; target=" + (Target is null ? "absent" : "present"));
    }
    public void SelectMonitor(MonitorInfo monitor, bool enable = false)
    {
        Config.TargetDevicePath = monitor.DevicePath; if (enable) Config.Enabled = true;
        states.Clear(); Save(); RefreshMonitor(); Scan();
    }
    public void Toggle()
    {
        Config.Enabled = !Config.Enabled; dragging = 0; previewTimer.Stop(); overlay.Hide();
        foreach (var s in states.Values) s.LogicalMaximized = false;
        Save(); RefreshMonitor();
        if (Config.Enabled) Scan();
    }
    public void SelectLayout(string id)
    {
        var next = Layouts.FirstOrDefault(l => l.Id == id); if (next is null) return;
        var oldZones = Zones.ToList();
        if (Target is not null) _ = LayoutEngine.Calculate(next, TargetBounds, Config.MinimumZoneWidth, Config.MinimumZoneHeight);
        Config.ActiveLayoutId = id; Save(); RefreshMonitor();
        if (!Active) return;
        foreach (var (hwnd, state) in states.ToArray())
        {
            if (!CanTouch(hwnd)) continue;
            var old = oldZones.FirstOrDefault(z => z.Id == state.ZoneId);
            var zone = Zones.FirstOrDefault(z => z.Id == state.ZoneId)
                ?? Zones.FirstOrDefault(z => old is not null && string.Equals(z.Name, old.Name, StringComparison.OrdinalIgnoreCase))
                ?? (old is null ? null : Zones.OrderBy(z => Math.Pow(z.Bounds.CenterX - old.Bounds.CenterX, 2) + Math.Pow(z.Bounds.CenterY - old.Bounds.CenterY, 2)).FirstOrDefault());
            if (zone is not null) Snap(hwnd, zone, state.ManualOverride, state.LogicalMaximized);
        }
        Log.Write("Layout selected");
    }
    public void StoreLayout(LayoutDefinition layout)
    {
        _ = LayoutEngine.Calculate(layout, TargetBounds, Config.MinimumZoneWidth, Config.MinimumZoneHeight);
        Store.SaveLayout(layout); int at = Layouts.FindIndex(l => l.Id == layout.Id);
        if (at < 0) Layouts.Add(layout); else Layouts[at] = layout;
        SelectLayout(layout.Id);
    }
    public void DeleteLayout(LayoutDefinition layout)
    {
        if (Layouts.Count <= 1) throw new ArgumentException("Нельзя удалить последнюю раскладку.");
        if (Config.ActiveLayoutId == layout.Id) SelectLayout(Layouts.First(l => l.Id != layout.Id).Id);
        Store.DeleteLayout(layout); Layouts.Remove(layout); Changed?.Invoke();
    }
    bool CanTouch(nint hwnd) => Active && NativeMethods.IsDefaultDesktop() && Target is not null && MonitorManager.IsTarget(hwnd, Target) && WindowManager.Eligible(hwnd, Config);
    WindowRuntimeState Track(nint hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (!states.TryGetValue(hwnd, out var s) || s.ProcessId != pid)
        {
            var rect = WindowManager.Bounds(hwnd) ?? default;
            var zone = LayoutEngine.Detect(Zones, rect);
            s = new() { ProcessId = pid, PreviousRect = rect, LastRect = rect, ZoneId = zone?.Id ?? "", LayoutId = Layout.Id };
            states[hwnd] = s;
        }
        return s;
    }
    public void Scan()
    {
        foreach (var hwnd in states.Keys.Where(h => !NativeMethods.IsWindow(h)).ToArray()) states.Remove(hwnd);
        if (!Active) return;
        foreach (var hwnd in WindowManager.Enumerate()) if (CanTouch(hwnd)) { Track(hwnd); ApplyRule(hwnd); }
    }
    void ApplyRule(nint hwnd)
    {
        if (!CanTouch(hwnd)) return;
        var s = Track(hwnd); if (s.InitialRuleChecked || s.ManualOverride) return;
        var identity = WindowManager.Identity(hwnd); if (identity is null) return;
        var rule = RuleMatcher.Match(Config.Rules, identity, WindowManager.Title(hwnd), Layout.Id, s.ManualOverride);
        // Retry unmatched rules on foreground/show, because a window title may not exist at creation.
        if (rule is null) return;
        s.InitialRuleChecked = true;
        var zone = Zones.FirstOrDefault(z => z.Id == rule.ZoneId); if (zone is not null) Snap(hwnd, zone, false, false);
    }
    public bool Snap(nint hwnd, Zone zone, bool manual = true, bool maximize = false)
    {
        if (!CanTouch(hwnd)) return false;
        var s = Track(hwnd); var before = WindowManager.Bounds(hwnd);
        if (before is null) return false;
        if (!s.LogicalMaximized && !NativeMethods.IsZoomed(hwnd)) s.PreviousRect = before.Value.FitInside(zone.Bounds);
        bool ok = Place(hwnd, zone.Bounds, s);
        if (ok) { s.ZoneId = zone.Id; s.LayoutId = Layout.Id; s.LogicalMaximized = maximize; s.ManualOverride |= manual; Log.Write("Window snapped"); }
        return ok;
    }
    bool Place(nint hwnd, PixelRect rect, WindowRuntimeState state)
    {
        if (!CanTouch(hwnd)) return false;
        mutating = true;
        try
        {
            bool ok = WindowManager.Place(hwnd, rect);
            state.SuppressThrough = unchecked((uint)Environment.TickCount + 150u);
            state.LastRect = WindowManager.Bounds(hwnd) ?? rect;
            return ok;
        }
        finally { mutating = false; }
    }
    public void Restore(nint hwnd)
    {
        if (!CanTouch(hwnd) || !states.TryGetValue(hwnd, out var s)) return;
        var zone = Zones.FirstOrDefault(z => z.Id == s.ZoneId); if (zone is null) return;
        if (Place(hwnd, s.PreviousRect.FitInside(zone.Bounds), s)) s.LogicalMaximized = false;
    }
    public void Execute(string command) => ExecuteFor(command, NativeMethods.GetForegroundWindow());
    public void ExecuteFor(string command, nint hwnd)
    {
        try
        {
            if (command == "toggle") { Toggle(); return; }
            if (command.StartsWith("layout:", StringComparison.Ordinal)) { SelectLayout(command[7..]); return; }
            if (!CanTouch(hwnd)) return;
            if (command.StartsWith("zone:", StringComparison.Ordinal) && int.TryParse(command[5..], out int number))
            { if (number >= 1 && number <= Zones.Count) Snap(hwnd, Zones[number - 1]); return; }
            if (command == "restore") { Restore(hwnd); return; }
            var s = Track(hwnd); var from = Zones.FirstOrDefault(z => z.Id == s.ZoneId) ?? LayoutEngine.Detect(Zones, WindowManager.Bounds(hwnd) ?? default);
            if (from is null) return;
            if (command == "maximize") { if (s.LogicalMaximized) Restore(hwnd); else Snap(hwnd, from, true, true); return; }
            if (Enum.TryParse<Direction>(command, true, out var direction) && LayoutEngine.Neighbor(Zones, from, direction) is { } next) Snap(hwnd, next);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { Notice?.Invoke(ex.Message); }
    }
    void OnEvent(uint kind, nint hwnd, uint eventTime)
    {
        if (disposed) return;
        if (kind == NativeMethods.Destroy) { states.Remove(hwnd); if (dragging == hwnd) { dragging = 0; overlay.Hide(); } return; }
        if (kind == NativeMethods.Foreground && WindowManager.Eligible(hwnd, Config)) LastForeground = hwnd;
        if (mutating || !Active) return;
        if (kind == NativeMethods.MoveEnd)
        {
            if (dragging != hwnd) return;
            dragging = 0; overlay.Hide();
            if (!CanTouch(hwnd)) { states.Remove(hwnd); return; }
            var s = Track(hwnd); s.ManualOverride = true; s.LogicalMaximized = false;
            if (Config.AutoSnap && !dragResized && NativeMethods.GetCursorPos(out var pt) && Zones.FirstOrDefault(z => z.Bounds.Contains(pt.X, pt.Y)) is { } zone) Snap(hwnd, zone);
            else { s.LastRect = WindowManager.Bounds(hwnd) ?? s.LastRect; s.ZoneId = LayoutEngine.Detect(Zones, s.LastRect)?.Id ?? ""; }
            return;
        }
        if (kind == NativeMethods.MoveStart)
        {
            // Track only eligible moves that start on the selected monitor; main monitor stays untouched.
            if (!CanTouch(hwnd)) return;
            previewTimer.Stop();
            dragging = hwnd; dragStartRect = WindowManager.Bounds(hwnd) ?? default; dragResized = false; var s = Track(hwnd); s.ManualOverride = true; s.LogicalMaximized = false; ShowOverlay(hwnd); return;
        }
        if (kind == NativeMethods.Location && dragging == hwnd)
        {
            if (WindowManager.Bounds(hwnd) is { } moved && (moved.Width != dragStartRect.Width || moved.Height != dragStartRect.Height)) dragResized = true;
            if (dragResized) overlay.Hide(); else ShowOverlay(hwnd); return;
        }
        if (!CanTouch(hwnd)) return;
        var state = Track(hwnd);
        if (kind is NativeMethods.Show or NativeMethods.Foreground) ApplyRule(hwnd);
        if (kind is not (NativeMethods.Location or NativeMethods.State)) return;
        if (NativeMethods.IsZoomed(hwnd))
        {
            var zone = Zones.FirstOrDefault(z => z.Id == state.ZoneId) ?? LayoutEngine.Detect(Zones, state.LastRect);
            if (zone is null) return;
            if (state.LogicalMaximized) Restore(hwnd);
            else { state.PreviousRect = state.LastRect.FitInside(zone.Bounds); Snap(hwnd, zone, false, true); }
            Log.Write("Native maximize handled");
        }
        else if (state.SuppressThrough == 0 || unchecked((int)(eventTime - state.SuppressThrough)) > 0)
        {
            var rect = WindowManager.Bounds(hwnd); if (rect is null) return;
            if (rect.Value != state.LastRect) { state.LogicalMaximized = false; state.LastRect = rect.Value; state.ZoneId = LayoutEngine.Detect(Zones, rect.Value)?.Id ?? ""; }
        }
    }
    void ShowOverlay(nint hwnd)
    {
        if (!Config.AutoSnap || Target is null || !NativeMethods.IsDefaultDesktop() || !NativeMethods.GetCursorPos(out var p) || !TargetBounds.Contains(p.X, p.Y)) { overlay.Hide(); return; }
        overlay.Display(TargetBounds, Zones, Zones.FirstOrDefault(z => z.Bounds.Contains(p.X, p.Y))?.Id);
    }
    public void SaveWorkspace()
    {
        if (!Active) throw new InvalidOperationException("Включите QuadDesk и выберите дисплей.");
        var workspace = new Workspace { LayoutId = Layout.Id }; var occurrences = new Dictionary<WindowIdentity, int>();
        foreach (var hwnd in WindowManager.Enumerate())
        {
            if (!CanTouch(hwnd)) continue;
            var s = Track(hwnd); var zone = Zones.FirstOrDefault(z => z.Id == s.ZoneId); var identity = WindowManager.Identity(hwnd);
            if (zone is null || identity is null) continue;
            int occurrence = occurrences.GetValueOrDefault(identity); occurrences[identity] = occurrence + 1;
            var r = s.LogicalMaximized ? s.PreviousRect.FitInside(zone.Bounds) : (WindowManager.Bounds(hwnd) ?? s.LastRect).FitInside(zone.Bounds); var z = zone.Bounds;
            workspace.Windows.Add(new() { Identity = identity, Occurrence = occurrence, ZoneId = zone.Id, X = (r.X - z.X) / (double)z.Width, Y = (r.Y - z.Y) / (double)z.Height, Width = r.Width / (double)z.Width, Height = r.Height / (double)z.Height, LogicalMaximized = s.LogicalMaximized });
        }
        Storage.Save(Path.Combine(Store.Root, "workspaces", "default.json"), workspace);
    }
    public void TryRestoreWorkspace()
    {
        try { RestoreWorkspace(); } catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException) { Notice?.Invoke("Workspace не восстановлен: " + ex.Message); }
    }
    void RestoreWorkspace()
    {
        if (!Active) throw new InvalidOperationException("Включите QuadDesk и выберите дисплей.");
        var workspace = Storage.Read<Workspace>(Path.Combine(Store.Root, "workspaces", "default.json"));
        if (workspace.Windows is null || !Layouts.Any(l => l.Id == workspace.LayoutId)) throw new InvalidDataException("Неверный workspace.");
        foreach (var w in workspace.Windows)
            if (w is null || w.Identity is null || w.Occurrence < 0 || string.IsNullOrWhiteSpace(w.ZoneId) || new[] { w.X, w.Y, w.Width, w.Height }.Any(v => !double.IsFinite(v) || v < 0 || v > 1) || w.Width <= 0 || w.Height <= 0) throw new InvalidDataException("Неверная геометрия workspace.");
        SelectLayout(workspace.LayoutId);
        var available = WindowManager.Enumerate().Where(CanTouch).Select(h => (Hwnd: h, Identity: WindowManager.Identity(h))).Where(x => x.Identity is not null).GroupBy(x => x.Identity!).ToDictionary(g => g.Key, g => g.Select(x => x.Hwnd).ToList());
        foreach (var w in workspace.Windows)
        {
            var zone = Zones.FirstOrDefault(z => z.Id == w.ZoneId);
            if (zone is null || !available.TryGetValue(w.Identity, out var windows) || w.Occurrence >= windows.Count) continue;
            nint hwnd = windows[w.Occurrence]; var z = zone.Bounds; var s = Track(hwnd);
            var r = new PixelRect(z.X + (int)Math.Round(w.X * z.Width), z.Y + (int)Math.Round(w.Y * z.Height), Math.Max(1, (int)Math.Round(w.Width * z.Width)), Math.Max(1, (int)Math.Round(w.Height * z.Height))).FitInside(z);
            if (Place(hwnd, w.LogicalMaximized ? z : r, s)) { s.ZoneId = zone.Id; s.LayoutId = Layout.Id; s.PreviousRect = r; s.LogicalMaximized = w.LogicalMaximized; s.ManualOverride = true; }
        }
    }
    public void Dispose()
    {
        disposed = true; previewTimer.Dispose(); hooks.Dispose(); overlay.Dispose(); host.Command -= Execute; host.TopologyChanged -= RefreshMonitor; host.LockChanged -= OnLock; states.Clear();
    }
}
