using System.Text.Json;
using QuadDesk.Core;
using QuadDesk.Native;
using QuadDesk.Services;
namespace QuadDesk.UI;

internal static class Dialogs
{
    public static string? Ask(IWin32Window owner, string title, string initial)
    {
        using var form = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, ClientSize = new(440, 110), MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog, AutoScaleMode = AutoScaleMode.Dpi };
        var input = new TextBox { Text = initial, Left = 16, Top = 18, Width = 408 };
        var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Left = 224, Top = 65, Width = 100 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 330, Top = 65, Width = 94 };
        form.Controls.AddRange([input, ok, cancel]); form.AcceptButton = ok; form.CancelButton = cancel;
        return form.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrWhiteSpace(input.Text) ? input.Text.Trim() : null;
    }
    public static MonitorInfo? SelectMonitor(IWin32Window? owner)
    {
        using var form = new Form { Text = "Выберите дисплей для QuadDesk", ClientSize = new(820, 290), StartPosition = FormStartPosition.CenterScreen, AutoScaleMode = AutoScaleMode.Dpi };
        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new(8), FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "Выбрать и включить зоны", AutoSize = true }; var refresh = new Button { Text = "Обновить", AutoSize = true }; var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        void Load() { list.Items.Clear(); list.Items.AddRange(MonitorManager.GetAll().Cast<object>().ToArray()); }
        refresh.Click += (_, _) => Load(); ok.Click += (_, _) => { if (list.SelectedItem is MonitorInfo) { form.DialogResult = DialogResult.OK; form.Close(); } };
        bottom.Controls.AddRange([cancel, ok, refresh]); form.Controls.Add(list); form.Controls.Add(bottom); form.CancelButton = cancel; Load();
        return form.ShowDialog(owner) == DialogResult.OK ? list.SelectedItem as MonitorInfo : null;
    }
    public static void Error(IWin32Window? owner, Exception ex) { Log.Write("UI error: " + ex.GetType().Name); MessageBox.Show(owner, ex.Message, "QuadDesk", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
}
internal sealed class LayoutEditorForm : Form
{
    readonly LayoutPreviewControl preview;
    readonly Label details = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 58, Padding = new(12) };
    readonly LayoutDefinition original;
    readonly TextBox name;
    public LayoutDefinition Result { get; private set; }
    public LayoutEditorForm(LayoutDefinition source, PixelRect bounds, int minW, int minH)
    {
        original = JsonData.Clone(source); Result = JsonData.Clone(source);
        Text = "Редактор раскладки — QuadDesk"; ClientSize = new(980, 680); MinimumSize = new(780, 520); StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        preview = new() { Dock = DockStyle.Fill, Editable = true, CurrentLayout = Result, Monitor = bounds, MinimumWidth = minW, MinimumHeight = minH };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 100, Padding = new(8), AutoScroll = true };
        name = new() { Text = Result.Name, Width = 210 };
        actions.Controls.Add(name);
        void Button(string label, Action action) { var b = new Button { Text = label, AutoSize = true }; b.Click += (_, _) => { try { action(); } catch (Exception ex) { Dialogs.Error(this, ex); } }; actions.Controls.Add(b); }
        void Modify(Action<LayoutDefinition> edit)
        {
            var candidate = JsonData.Clone(Result); edit(candidate); _ = QuadDesk.Core.LayoutEngine.Calculate(candidate, bounds, minW, minH);
            Result = candidate; preview.CurrentLayout = Result; preview.Invalidate(); Details();
        }
        void Split(SplitOrientation orientation)
        {
            if (preview.SelectedZoneId is not { } id) return;
            Modify(l => l.Root = QuadDesk.Core.LayoutEngine.Replace(l.Root, id, z => new SplitNode { Orientation = orientation, First = z, Second = new ZoneNode { Name = "Новая зона" } }));
        }
        Button("Разделить ↔", () => Split(SplitOrientation.Vertical));
        Button("Разделить ↕", () => Split(SplitOrientation.Horizontal));
        Button("Удалить / объединить", () => { if (preview.SelectedZoneId is { } id) Modify(l => l.Root = QuadDesk.Core.LayoutEngine.Remove(l.Root, id)); });
        Button("Имя зоны", () =>
        {
            var zone = QuadDesk.Core.LayoutEngine.Calculate(Result, bounds).FirstOrDefault(z => z.Id == preview.SelectedZoneId); if (zone is null) return;
            var value = Dialogs.Ask(this, "Имя зоны", zone.Name); if (value is not null) Modify(l => l.Root = QuadDesk.Core.LayoutEngine.Replace(l.Root, zone.Id, z => { z.Name = value; return z; }));
        });
        Button("Сброс изменений", () => { Result = JsonData.Clone(original); preview.CurrentLayout = Result; name.Text = Result.Name; preview.Invalidate(); Details(); });
        Button("Сохранить", () => Finish(false)); Button("Сохранить как…", () => Finish(true));
        Button("Отмена", () => { DialogResult = DialogResult.Cancel; Close(); });
        preview.SelectionChanged += Details; preview.TreeChanged += Details;
        Controls.Add(preview); Controls.Add(details); Controls.Add(actions); Details();
    }
    void Finish(bool copy)
    {
        if (string.IsNullOrWhiteSpace(name.Text)) throw new ArgumentException("Введите имя раскладки.");
        Result.Name = name.Text.Trim(); if (copy) { Result.Id = Guid.NewGuid().ToString("N"); Result.CreatedAt = DateTimeOffset.UtcNow; }
        QuadDesk.Core.LayoutEngine.Validate(Result); DialogResult = DialogResult.OK; Close();
    }
    void Details()
    {
        var zone = QuadDesk.Core.LayoutEngine.Calculate(Result, preview.Monitor).FirstOrDefault(z => z.Id == preview.SelectedZoneId);
        details.Text = zone is null ? "Выберите зону. Для изменения пропорций перетащите границу. Изменения применяются к окнам после сохранения." : $"ID: {zone.Id}\n{zone.Bounds.Width} × {zone.Bounds.Height} px · ширина {zone.Bounds.Width * 100d / preview.Monitor.Width:F1}% · высота {zone.Bounds.Height * 100d / preview.Monitor.Height:F1}%";
    }
}
internal sealed class SettingsForm : Form
{
    public AppConfig Result { get; private set; }
    readonly CheckBox full, snap, smart, strict, winArrow, restore, cursorFinder, cursorFinderFullscreen;
    readonly NumericUpDown width, height, guard, cursorSensitivity, cursorSize, cursorFade;
    readonly TextBox exclude;
    readonly HotkeysEditor hotkeysEditor;
    public SettingsForm(QuadDeskController controller)
    {
        var source = controller.Config; hotkeysEditor = new(controller) { Dock = DockStyle.Fill };
        Result = JsonData.Clone(source); Text = "Настройки QuadDesk"; ClientSize = new(760, 760); StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var general = new TabPage("Общие"); var hotkeys = new TabPage("Горячие клавиши"); tabs.TabPages.AddRange([general, hotkeys]);
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new(16), AutoScroll = true };
        full = new() { Text = "Использовать весь монитор, включая область панели задач", Checked = source.UseFullMonitorBounds, AutoSize = true };
        snap = new() { Text = "Привязка при завершении перетаскивания на выбранном дисплее", Checked = source.AutoSnap, AutoSize = true };
        smart = new() { Text = "Smart Snap: Windows Snap и границы работают внутри каждой зоны", Checked = source.SmartSnap, AutoSize = true };
        strict = new() { Text = "Strict Submonitors: жёстко удерживать окна внутри подмониторов", Checked = source.StrictSubmonitors, AutoSize = true };
        winArrow = new() { Text = "Перехватывать Win+Arrow на выбранном мониторе", Checked = source.CaptureWinArrow, AutoSize = true };
        restore = new() { Text = "Восстанавливать workspace при запуске", Checked = source.RestoreWorkspaceOnStart, AutoSize = true };
        cursorFinder = new() { Text = "Cursor Finder: увеличивать курсор при резкой тряске мыши", Checked = source.CursorFinderEnabled, AutoSize = true };
        cursorFinderFullscreen = new() { Text = "Не показывать Cursor Finder поверх full-screen/игр", Checked = source.CursorFinderSuppressFullscreen, AutoSize = true };
        width = new() { Minimum = 40, Maximum = 4000, Value = source.MinimumZoneWidth, Width = 120 };
        height = new() { Minimum = 40, Maximum = 4000, Value = source.MinimumZoneHeight, Width = 120 };
        guard = new() { Minimum = 50, Maximum = 1000, Increment = 25, Value = Math.Clamp(source.GuardIntervalMs, 50, 1000), Width = 120 };
        cursorSensitivity = new() { Minimum = 200, Maximum = 3000, Increment = 50, Value = Math.Clamp(source.CursorFinderSensitivity, 200, 3000), Width = 120 };
        cursorSize = new() { Minimum = 48, Maximum = 256, Increment = 8, Value = Math.Clamp(source.CursorFinderMaxSize, 48, 256), Width = 120 };
        cursorFade = new() { Minimum = 150, Maximum = 3000, Increment = 50, Value = Math.Clamp(source.CursorFinderFadeMs, 150, 3000), Width = 120 };
        exclude = new() { Multiline = true, Width = 650, Height = 130, ScrollBars = ScrollBars.Vertical, Text = string.Join(Environment.NewLine, source.ExcludedProcesses) };
        flow.Controls.AddRange([full, snap, smart, strict, winArrow, restore,
            new Label { Text = "Cursor Finder", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) },
            cursorFinder,
            cursorFinderFullscreen,
            new Label { Text = "Чувствительность Cursor Finder: меньше = легче срабатывает", AutoSize = true }, cursorSensitivity,
            new Label { Text = "Максимальный размер визуального курсора (px)", AutoSize = true }, cursorSize,
            new Label { Text = "Затухание Cursor Finder (ms)", AutoSize = true }, cursorFade,
            new Label { Text = "Частота Strict Guard (ms)", AutoSize = true }, guard, new Label { Text = "Минимальная ширина зоны (px)", AutoSize = true }, width, new Label { Text = "Минимальная высота зоны (px)", AutoSize = true }, height, new Label { Text = "Исключения: имя процесса на строку (например game.exe)", AutoSize = true }, exclude,
            new Label { Text = "Панель задач на ТВ: Параметры Windows → Персонализация →\nПанель задач → Поведение → показывать на всех дисплеях: выкл.\nСистемный круг по Ctrl отключается отдельно: Панель управления → Мышь → Параметры указателя.", AutoSize = true }]);
        general.Controls.Add(flow); hotkeys.Controls.Add(hotkeysEditor);
        var save = new Button { Dock = DockStyle.Bottom, Text = "Сохранить", Height = 42 }; save.Click += (_, _) =>
        {
            try
            {
                Result.UseFullMonitorBounds = full.Checked; Result.AutoSnap = snap.Checked; Result.SmartSnap = smart.Checked; Result.StrictSubmonitors = strict.Checked; Result.CaptureWinArrow = winArrow.Checked; Result.RestoreWorkspaceOnStart = restore.Checked;
                Result.CursorFinderEnabled = cursorFinder.Checked; Result.CursorFinderSuppressFullscreen = cursorFinderFullscreen.Checked;
                Result.GuardIntervalMs = (int)guard.Value; Result.MinimumZoneWidth = (int)width.Value; Result.MinimumZoneHeight = (int)height.Value;
                Result.CursorFinderSensitivity = (int)cursorSensitivity.Value; Result.CursorFinderMaxSize = (int)cursorSize.Value; Result.CursorFinderFadeMs = (int)cursorFade.Value;
                Result.ExcludedProcesses = exclude.Lines.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
                Result.Hotkeys = hotkeysEditor.ReadBindings();
                Storage.ValidateConfig(Result); DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { Dialogs.Error(this, ex); }
        };
        Controls.Add(tabs); Controls.Add(save);
    }
    internal static DataGridView Grid() => new() { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersWidth = 26, BackgroundColor = SystemColors.Window };
}
internal sealed class RulesForm : Form
{
    public List<WindowRule> Result { get; private set; }
    readonly DataGridView grid = SettingsForm.Grid();
    public RulesForm(AppConfig config, IReadOnlyList<LayoutDefinition> layouts)
    {
        Result = JsonData.Clone(config.Rules); Text = "Правила первого размещения"; ClientSize = new(1050, 520); StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        foreach (var (id, label) in new[] { ("process", "Процесс.exe"), ("class", "Класс (опц.)"), ("title", "Заголовок содержит"), ("layout", "Layout ID"), ("zone", "Zone ID") }) grid.Columns.Add(id, label);
        foreach (var r in Result) grid.Rows.Add(r.ProcessName, r.WindowClass ?? string.Empty, r.TitleContains ?? string.Empty, r.LayoutId, r.ZoneId);
        var help = new TextBox { Dock = DockStyle.Top, Height = 110, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = "Правила действуют только на выбранном мониторе; ручное перемещение приоритетнее.\r\n" + string.Join("\r\n", layouts.Select(l => l.Id + ": " + string.Join(", ", QuadDesk.Core.LayoutEngine.Calculate(l, new(0, 0, 16384, 16384)).Select(z => z.Id)))) };
        var save = new Button { Dock = DockStyle.Bottom, Height = 42, Text = "Сохранить правила" };
        save.Click += (_, _) =>
        {
            try
            {
                grid.EndEdit(); var rules = new List<WindowRule>();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue; string V(int i) => row.Cells[i].Value?.ToString()?.Trim() ?? "";
                    var r = new WindowRule { ProcessName = V(0), WindowClass = V(1), TitleContains = V(2), LayoutId = V(3), ZoneId = V(4) };
                    var l = layouts.FirstOrDefault(l => l.Id == r.LayoutId);
                    if (r.ProcessName.Length == 0 || l is null || !QuadDesk.Core.LayoutEngine.Calculate(l, new(0, 0, 16384, 16384)).Any(z => z.Id == r.ZoneId)) throw new ArgumentException("Укажите процесс и существующие Layout ID / Zone ID.");
                    rules.Add(r);
                }
                Result = rules; DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { Dialogs.Error(this, ex); }
        };
        Controls.Add(grid); Controls.Add(help); Controls.Add(save);
    }
}
