using System.Diagnostics;
using QuadDesk.Services;
using QuadDesk.UI;
namespace QuadDesk;
internal sealed class QuadDeskApplicationContext : ApplicationContext
{
    readonly EventHost host = new();
    readonly QuadDeskController controller;
    readonly StrictSubmonitorService strict;
    readonly WindowsSnapService windowsSnap;
    readonly CursorFinderService cursorFinder;
    readonly MainForm main;
    readonly NotifyIcon tray;
    readonly Icon trayIcon;
    readonly ContextMenuStrip menu = new();
    nint menuForeground;
    public QuadDeskApplicationContext()
    {
        controller = new(new Storage(), host);
        strict = new(controller);
        windowsSnap = new(controller);
        cursorFinder = new(controller);
        host.InterceptCommand = strict.HandleWinCommand;
        main = new(controller);
        trayIcon = (Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application).Clone() as Icon ?? SystemIcons.Application;
        tray = new() { Text = "QuadDesk", Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowMain();
        controller.Notice += Notice;
        menu.Opening += (_, _) => { menuForeground = controller.LastForeground; BuildMenu(); };
        controller.Start(); Log.Write("Application start");
        main.Show();
        if (controller.Config.TargetDevicePath is null) main.Safe(main.SelectMonitor);
    }
    void ShowMain() { main.Show(); main.WindowState = FormWindowState.Normal; main.Activate(); }
    void Notice(string text) { tray.ShowBalloonTip(6000, "QuadDesk", text, ToolTipIcon.Warning); }
    void ToggleStrict()
    {
        controller.Config.StrictSubmonitors = !controller.Config.StrictSubmonitors;
        controller.Save();
    }
    void ToggleWinArrow()
    {
        controller.Config.CaptureWinArrow = !controller.Config.CaptureWinArrow;
        controller.Save();
    }
    void ToggleWindowsSnap()
    {
        controller.Config.SuspendWindowsSnap = !controller.Config.SuspendWindowsSnap;
        controller.Save();
        windowsSnap.Sync();
    }
    void ToggleCursorFinder()
    {
        controller.Config.CursorFinderEnabled = !controller.Config.CursorFinderEnabled;
        controller.Save();
        cursorFinder.Sync();
    }
    void SetCursorFinderColor(string color)
    {
        controller.Config.CursorFinderColor = color;
        controller.Save();
        cursorFinder.Sync();
    }
    void BuildMenu()
    {
        foreach (ToolStripItem item in menu.Items.Cast<ToolStripItem>().ToArray()) { menu.Items.Remove(item); item.Dispose(); }
        ToolStripMenuItem Add(string text, Action action, bool check = false)
        { var item = new ToolStripMenuItem(text) { Checked = check }; item.Click += (_, _) => main.Safe(action); menu.Items.Add(item); return item; }
        Add("Открыть QuadDesk", ShowMain); Add(controller.Config.Enabled ? "Выключить зоны" : "Включить зоны", controller.Toggle, controller.Config.Enabled);
        Add("Показать зоны (5 сек)", controller.PreviewZones);
        Add("Разместить окна по зонам", controller.ArrangeWindows);
        Add("Горячие клавиши…", () => { ShowMain(); main.Hotkeys(); });
        var layouts = new ToolStripMenuItem("Раскладка"); menu.Items.Add(layouts);
        foreach (var layout in controller.Layouts)
        { var item = new ToolStripMenuItem(layout.Name) { Checked = layout.Id == controller.Config.ActiveLayoutId }; item.Click += (_, _) => main.Safe(() => controller.SelectLayout(layout.Id)); layouts.DropDownItems.Add(item); }
        var move = new ToolStripMenuItem("Переместить активное окно"); menu.Items.Add(move);
        for (int i = 0; i < controller.Zones.Count; i++)
        { int number = i + 1; var item = new ToolStripMenuItem($"{number}. {controller.Zones[i].Name}"); item.Click += (_, _) => controller.ExecuteFor($"zone:{number}", menuForeground); move.DropDownItems.Add(item); }
        Add("Развернуть / восстановить в зоне", () => controller.ExecuteFor("maximize", menuForeground));
        Add("Восстановить размер", () => controller.ExecuteFor("restore", menuForeground));
        Add("Автопривязка", () => { controller.Config.AutoSnap = !controller.Config.AutoSnap; controller.Save(); }, controller.Config.AutoSnap);
        Add("Cursor Finder", ToggleCursorFinder, controller.Config.CursorFinderEnabled);
        var cursorColors = new ToolStripMenuItem("Цвет Cursor Finder");
        menu.Items.Add(cursorColors);
        void CursorColor(string name, string color)
        {
            var item = new ToolStripMenuItem(name) { Checked = string.Equals(controller.Config.CursorFinderColor, color, StringComparison.OrdinalIgnoreCase) };
            item.Click += (_, _) => main.Safe(() => SetCursorFinderColor(color));
            cursorColors.DropDownItems.Add(item);
        }
        CursorColor("Голубой", "#00A2FF");
        CursorColor("Зелёный", "#00D084");
        CursorColor("Жёлтый", "#FFD400");
        CursorColor("Оранжевый", "#FF8A00");
        CursorColor("Красный", "#FF3B30");
        CursorColor("Фиолетовый", "#A855F7");
        CursorColor("Белый", "#FFFFFF");
        Add("Strict Submonitors", ToggleStrict, controller.Config.StrictSubmonitors);
        Add("Win+Arrow внутри зон", ToggleWinArrow, controller.Config.CaptureWinArrow);
        Add("Отключать системный Windows Snap", ToggleWindowsSnap, controller.Config.SuspendWindowsSnap);
        menu.Items.Add(new ToolStripSeparator());
        Add("Редактор раскладки…", () => { ShowMain(); main.Edit(); });
        Add("Правила…", () => { ShowMain(); main.Rules(); }); Add("Настройки…", () => { ShowMain(); main.Settings(); }); Add("Обновления…", () => { ShowMain(); main.Updates(); });
        Add("Выбрать дисплей…", main.SelectMonitor); Add("Сохранить workspace", controller.SaveWorkspace); Add("Восстановить workspace", controller.TryRestoreWorkspace);
        Add("Запускать с Windows", () => StartupService.Set(!StartupService.Enabled), StartupService.Enabled);
        Add("Открыть config.json", () => { controller.Save(); Process.Start(new ProcessStartInfo(controller.Store.ConfigPath) { UseShellExecute = true }); });
        Add("О программе", () => MessageBox.Show(main, $"{AppInfo.DisplayName}\nЛогические зоны одного физического дисплея.\nSource-available. Без телеметрии.\nГорячие клавиши настраиваются через меню.\nCursor Finder увеличивает курсор при резкой тряске мыши, поддерживает выбор цвета и подавляется поверх full-screen.\nПеред играми выключайте QuadDesk.\nИзменения config.json вручную применяются после перезапуска.", "QuadDesk"));
        menu.Items.Add(new ToolStripSeparator()); Add("Выход", ExitThread);
    }
    protected override void ExitThreadCore()
    {
        tray.Visible = false; cursorFinder.Dispose(); windowsSnap.Dispose(); strict.Dispose(); controller.Dispose(); main.Dispose(); tray.Dispose(); trayIcon.Dispose(); menu.Dispose(); host.Dispose(); base.ExitThreadCore();
    }
}
