using QuadDesk.Core;
using QuadDesk.Services;
namespace QuadDesk.UI;
internal sealed class MainForm : Form
{
    readonly QuadDeskController controller;
    readonly LayoutPreviewControl preview = new() { Dock = DockStyle.Fill };
    readonly Label status = new() { Dock = DockStyle.Top, Height = 145, Padding = new(16), Font = new("Segoe UI", 11) };
    readonly ComboBox layouts = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    readonly Button toggle = new() { AutoSize = true, Font = new("Segoe UI", 10, FontStyle.Bold) };
    bool updating;
    public MainForm(QuadDeskController controller)
    {
        this.controller = controller; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Text = $"{AppInfo.DisplayName} — логические зоны дисплея"; ClientSize = new(1050, 680); MinimumSize = new(820, 540); StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 145, Padding = new(12), AutoScroll = true };
        actions.Controls.Add(layouts);
        void B(string title, Action action) { var b = new Button { Text = title, AutoSize = true }; b.Click += (_, _) => Safe(action); actions.Controls.Add(b); }
        toggle.Click += (_, _) => Safe(() => { if (controller.Target is null) SelectMonitor(); else { controller.Toggle(); if (controller.Active) controller.PreviewZones(); } });
        actions.Controls.Add(toggle);
        B("Выбрать дисплей…", SelectMonitor); B("Показать зоны (5 сек)", controller.PreviewZones);
        B("Разместить окна по зонам", controller.ArrangeWindows); B("Горячие клавиши…", Hotkeys); B("Редактировать…", Edit);
        B("Новая раскладка…", () => EditLayout(new LayoutDefinition()));
        B("Дублировать", () => { var l = JsonData.Clone(controller.Layout); l.Id = Guid.NewGuid().ToString("N"); l.Name += " — копия"; l.CreatedAt = DateTimeOffset.UtcNow; controller.StoreLayout(l); });
        B("Переименовать", () => { var value = Dialogs.Ask(this, "Имя раскладки", controller.Layout.Name); if (value is null) return; var l = JsonData.Clone(controller.Layout); l.Name = value; controller.StoreLayout(l); });
        B("Удалить", () => { if (MessageBox.Show(this, $"Удалить раскладку «{controller.Layout.Name}»?", "QuadDesk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) controller.DeleteLayout(controller.Layout); });
        B("Правила…", Rules); B("Настройки…", Settings); B("Обновления…", Updates); B("Сохранить workspace", () => { controller.SaveWorkspace(); MessageBox.Show(this, "Рабочее пространство сохранено.", "QuadDesk"); });
        B("Восстановить workspace", controller.TryRestoreWorkspace);
        B("Восстановить пресеты", () => { foreach (var l in Presets.All()) if (!controller.Layouts.Any(x => x.Id == l.Id)) controller.StoreLayout(l); });
        layouts.SelectedIndexChanged += (_, _) => { if (!updating && layouts.SelectedItem is LayoutDefinition l) Safe(() => controller.SelectLayout(l.Id)); };
        Controls.Add(preview); Controls.Add(status); Controls.Add(actions); controller.Changed += RefreshData; RefreshData();
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
    }
    public void Safe(Action action) { try { action(); } catch (Exception ex) { Dialogs.Error(this, ex); } }
    public void SelectMonitor()
    {
        if (Dialogs.SelectMonitor(this) is { } monitor)
        { controller.SelectMonitor(monitor, enable: true); controller.PreviewZones(); }
    }
    public void Hotkeys()
    {
        using var dialog = new HotkeysForm(controller);
        if (dialog.ShowDialog(this) == DialogResult.OK) controller.ApplyHotkeys(dialog.Result);
    }
    public void Edit() => EditLayout(controller.Layout);
    void EditLayout(LayoutDefinition layout)
    {
        using var editor = new LayoutEditorForm(layout, controller.TargetBounds, controller.Config.MinimumZoneWidth, controller.Config.MinimumZoneHeight);
        if (editor.ShowDialog(this) == DialogResult.OK) controller.StoreLayout(editor.Result);
    }
    public void Settings()
    {
        using var dialog = new SettingsForm(controller); if (dialog.ShowDialog(this) == DialogResult.OK) controller.ApplySettings(dialog.Result);
    }
    public void Updates()
    {
        using var dialog = new UpdateForm(); dialog.ShowDialog(this);
    }
    public void Rules()
    {
        using var dialog = new RulesForm(controller.Config, controller.Layouts);
        if (dialog.ShowDialog(this) == DialogResult.OK) { var config = JsonData.Clone(controller.Config); config.Rules = dialog.Result; controller.ApplySettings(config); }
    }
    void RefreshData()
    {
        if (IsDisposed) return;
        updating = true;
        try
        {
            layouts.Items.Clear(); layouts.Items.AddRange(controller.Layouts.Cast<object>().ToArray()); layouts.SelectedItem = controller.Layout;
            int failed = controller.HotkeyStatuses.Count(s => !s.Registered && !string.IsNullOrWhiteSpace(s.Binding));
            toggle.Text = controller.Config.Enabled && controller.Target is not null ? "Выключить зоны" : "Включить зоны";
            status.ForeColor = controller.Active ? Color.DarkGreen : Color.Firebrick;
            status.Text = $"{controller.Status}\n{controller.Target?.ToString() ?? "Дисплей не выбран или отключён"}\nРаскладка: {controller.Layout.Name} · зон: {controller.Zones.Count} · Smart Snap: {(controller.Config.SmartSnap ? "вкл." : "выкл.")} · Strict: {(controller.Config.StrictSubmonitors ? "вкл." : "выкл.")} · Win+Arrow: {(controller.Config.CaptureWinArrow ? "вкл." : "выкл.")} · Windows Snap: {(controller.Config.SuspendWindowsSnap ? "подавлять" : "штатно")} · конфликтов клавиш: {failed}\nПеретащите окно к краю зоны для локального split или разверните его для локального maximize.";
            preview.CurrentLayout = controller.Layout; preview.Monitor = controller.TargetBounds; preview.Invalidate();
        }
        finally { updating = false; }
    }
    protected override void Dispose(bool disposing) { if (disposing) controller.Changed -= RefreshData; base.Dispose(disposing); }
}
