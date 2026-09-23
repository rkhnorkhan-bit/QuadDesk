using QuadDesk.Core;
namespace QuadDesk.UI;

internal sealed class HotkeysForm : Form
{
    readonly HotkeysEditor editor;
    public Dictionary<string, string> Result { get; private set; } = [];
    public HotkeysForm(QuadDeskController controller)
    {
        Text = "Горячие клавиши — QuadDesk"; ClientSize = new(920, 660); MinimumSize = new(780, 540);
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        editor = new(controller) { Dock = DockStyle.Fill };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new(8), FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Сохранить", AutoSize = true };
        save.Click += (_, _) => { try { Result = editor.ReadBindings(); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { Dialogs.Error(this, ex); } };
        buttons.Controls.AddRange([cancel, save]); Controls.Add(editor); Controls.Add(buttons); CancelButton = cancel;
    }
}

internal sealed class HotkeysEditor : UserControl
{
    readonly QuadDeskController controller;
    readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
    };
    bool loading;
    public HotkeysEditor(QuadDeskController controller)
    {
        this.controller = controller;
        var help = new Label { Dock = DockStyle.Top, Height = 82, Padding = new(8), Text = "Базовый набор: Ctrl+Alt+цифра — окно в зону; Ctrl+Alt+0 — включить / выключить.\nCtrl+Alt+Shift+цифра — раскладка. Любое сочетание можно изменить или очистить.\nДважды щёлкните комбинацию для ввода текста или нажмите «Записать сочетание». Проверка действует на текущий момент." };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 88, Padding = new(8), AutoScroll = true };
        void Button(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, _) => { try { action(); } catch (Exception ex) { Dialogs.Error(FindForm(), ex); } };
            actions.Controls.Add(button);
        }
        Button("Базовый набор", () => LoadBindings(AppConfig.DefaultHotkeys()));
        Button("Отключить все", () => LoadBindings(new Dictionary<string, string>()));
        Button("Очистить выбранное", () => { if (grid.CurrentRow is { } row) row.Cells[1].Value = ""; });
        Button("Записать сочетание…", RecordShortcut);
        Button("Проверить", () => ShowStatuses(controller.CheckHotkeys(ReadBindings())));
        grid.Columns.Add("action", "Действие"); grid.Columns[0].ReadOnly = true; grid.Columns[0].FillWeight = 105;
        grid.Columns.Add("binding", "Комбинация"); grid.Columns[1].FillWeight = 100;
        grid.Columns.Add("status", "Статус"); grid.Columns[2].ReadOnly = true; grid.Columns[2].FillWeight = 150;
        var actionsByName = AppConfig.DefaultHotkeys().Keys.Concat(controller.Config.Hotkeys.Keys)
            .Concat(controller.Layouts.Select(l => "layout:" + l.Id)).Distinct().ToArray();
        foreach (string action in actionsByName)
        { int index = grid.Rows.Add(LabelFor(action), "", ""); grid.Rows[index].Tag = action; }
        grid.CellValueChanged += (_, e) =>
        {
            if (!loading && e.RowIndex >= 0 && e.ColumnIndex == 1)
            { var row = grid.Rows[e.RowIndex]; row.Cells[2].Value = string.IsNullOrWhiteSpace(row.Cells[1].Value?.ToString()) ? "Отключено" : "Не проверено"; row.DefaultCellStyle.ForeColor = SystemColors.ControlText; }
        };
        Controls.Add(grid); Controls.Add(help); Controls.Add(actions);
        LoadBindings(controller.Config.Hotkeys); ShowStatuses(controller.HotkeyStatuses);
    }
    string LabelFor(string action)
    {
        if (action.StartsWith("zone:")) return "Окно → зона " + action[5..];
        if (action.StartsWith("layout:")) return "Раскладка: " + (controller.Layouts.FirstOrDefault(l => l.Id == action[7..])?.Name ?? action[7..]);
        return action switch
        {
            "toggle" => "Включить / выключить зоны", "restore" => "Восстановить размер окна", "maximize" => "Развернуть / восстановить в зоне",
            "left" => "Окно → соседняя зона слева", "right" => "Окно → соседняя зона справа", "up" => "Окно → зона сверху", "down" => "Окно → зона снизу", _ => action
        };
    }
    void LoadBindings(IReadOnlyDictionary<string, string> values)
    {
        grid.EndEdit(); loading = true;
        try
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                string value = values.TryGetValue((string)row.Tag!, out var binding) ? binding : "";
                row.Cells[1].Value = value; row.Cells[2].Value = value.Length == 0 ? "Отключено" : "Не проверено";
                row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
            }
        }
        finally { loading = false; }
    }
    public Dictionary<string, string> ReadBindings()
    {
        grid.EndEdit(); var result = new Dictionary<string, string>(); var seen = new HashSet<HotkeyChord>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            string value = row.Cells[1].Value?.ToString()?.Trim() ?? "";
            if (value.Length > 0)
            {
                if (!HotkeyChord.TryParse(value, out var chord)) throw new ArgumentException("Неверная комбинация: " + value + ". Пример: Ctrl+Alt+1.");
                if (!seen.Add(chord)) throw new ArgumentException("Одна комбинация назначена нескольким действиям: " + value);
            }
            result.Add((string)row.Tag!, value);
        }
        return result;
    }
    void ShowStatuses(IReadOnlyList<HotkeyStatus> statuses)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            var state = statuses.FirstOrDefault(s => s.Action == (string)row.Tag! && s.Binding == row.Cells[1].Value?.ToString());
            if (state is null) continue;
            row.Cells[2].Value = state.Message;
            row.DefaultCellStyle.ForeColor = state.Registered ? Color.DarkGreen : string.IsNullOrWhiteSpace(state.Binding) ? SystemColors.GrayText : Color.Firebrick;
        }
    }
    void RecordShortcut()
    {
        if (grid.CurrentRow is not { } row) return;
        grid.EndEdit(); controller.SuspendHotkeys();
        try
        {
            using var dialog = new Form { Text = "Запись сочетания", ClientSize = new(540, 190), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, AutoScaleMode = AutoScaleMode.Dpi };
            var help = new Label { Left = 16, Top = 12, Width = 508, Height = 55, Text = "Нажмите Ctrl, Alt или Shift вместе с нужной клавишей.\nСочетания с Win можно ввести текстом в таблице. Esc — отмена." };
            var input = new TextBox { Left = 16, Top = 78, Width = 508, ReadOnly = true };
            var ok = new Button { Left = 300, Top = 130, Width = 100, Text = "Применить", Enabled = false, DialogResult = DialogResult.OK };
            var cancel = new Button { Left = 410, Top = 130, Width = 114, Text = "Отмена", DialogResult = DialogResult.Cancel };
            string captured = "";
            input.PreviewKeyDown += (_, e) => e.IsInputKey = true;
            input.KeyDown += (_, e) =>
            {
                e.SuppressKeyPress = true; e.Handled = true;
                if (e.KeyCode == Keys.Escape && e.Modifiers == Keys.None) { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); return; }
                if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
                string key = e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ? ((char)e.KeyCode).ToString() : e.KeyCode.ToString();
                string value = (e.Control ? "Ctrl+" : "") + (e.Alt ? "Alt+" : "") + (e.Shift ? "Shift+" : "") + key;
                if (HotkeyChord.TryParse(value, out var recordedChord)) { captured = value; input.Text = value; ok.Enabled = true; }
            };
            dialog.Controls.AddRange([help, input, ok, cancel]); dialog.CancelButton = cancel; dialog.Shown += (_, _) => input.Focus();
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK) row.Cells[1].Value = captured;
        }
        finally { controller.ResumeHotkeys(); }
    }
}
