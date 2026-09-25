using QuadDesk.Services;

namespace QuadDesk.UI;

internal sealed class UpdateForm : Form
{
    readonly Label status = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(16),
        Font = new Font("Segoe UI", 10),
        Text = "Канал обновлений: stable GitHub Releases.\n\nНажмите «Проверить», чтобы найти новую стабильную версию."
    };

    readonly Button check = new() { Text = "Проверить", AutoSize = true };
    readonly Button install = new() { Text = "Скачать и установить", AutoSize = true, Enabled = false };
    readonly Button close = new() { Text = "Закрыть", AutoSize = true, DialogResult = DialogResult.Cancel };
    readonly CancellationTokenSource lifetime = new();

    UpdateCheckResult? available;

    public UpdateForm()
    {
        Text = "Обновления QuadDesk";
        ClientSize = new Size(620, 240);
        MinimumSize = new Size(560, 220);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(12),
            FlowDirection = FlowDirection.RightToLeft
        };
        bottom.Controls.AddRange([close, install, check]);

        check.Click += async (_, _) => await CheckAsync();
        install.Click += async (_, _) => await InstallAsync();

        Controls.Add(status);
        Controls.Add(bottom);
        CancelButton = close;
    }

    async Task CheckAsync()
    {
        SetBusy(true);
        try
        {
            status.Text = "Проверяю stable GitHub Releases...";
            available = await UpdaterService.CheckStableAsync(lifetime.Token);

            if (available.State == UpdateState.Available)
            {
                install.Enabled = true;
                status.Text =
                    $"Доступно stable-обновление.\n\n" +
                    $"Текущая версия: {available.CurrentVersion}\n" +
                    $"Новая версия: {available.LatestVersion}\n" +
                    $"Файл: {available.InstallerName}\n" +
                    $"Источник: stable GitHub Release\n\n" +
                    "При установке QuadDesk закроется, внешний updater проверит SHA-256, запустит установщик и затем откроет новую версию.";
            }
            else
            {
                install.Enabled = false;
                status.Text =
                    $"{available.Message}\n\n" +
                    $"Текущая версия: {available.CurrentVersion}\n" +
                    $"Последняя stable-версия: {available.LatestVersion}\n" +
                    $"Источник: stable GitHub Releases";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or InvalidOperationException or FormatException or TaskCanceledException)
        {
            install.Enabled = false;
            status.Text = "Не удалось проверить обновления.\n\n" + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task InstallAsync()
    {
        if (available is null || available.State != UpdateState.Available)
            return;

        if (MessageBox.Show(this,
                "QuadDesk скачает stable-обновление, проверит SHA-256 и закроется для установки. Продолжить?",
                "QuadDesk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(true);
        try
        {
            var progress = new Progress<string>(message => status.Text = message);
            await UpdaterService.DownloadAndStartStableUpdateAsync(available, progress, lifetime.Token);
            status.Text = "Updater запущен. QuadDesk сейчас закроется.";
            await Task.Delay(600, lifetime.Token);
            Application.Exit();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or TaskCanceledException)
        {
            status.Text = "Обновление не запущено.\n\n" + ex.Message;
            SetBusy(false);
        }
    }

    void SetBusy(bool busy)
    {
        check.Enabled = !busy;
        close.Enabled = !busy;
        if (busy) install.Enabled = false;
        else install.Enabled = available?.State == UpdateState.Available;
        UseWaitCursor = busy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) lifetime.Dispose();
        base.Dispose(disposing);
    }
}
