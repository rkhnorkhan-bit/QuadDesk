using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuadDesk.Services;

internal enum UpdateState { UpToDate, Available }

internal sealed record UpdateCheckResult(
    UpdateState State,
    string CurrentVersion,
    string LatestVersion,
    string? ReleaseUrl,
    string? InstallerName,
    string? InstallerUrl,
    string? ChecksumsUrl,
    string Message);

internal static class UpdaterService
{
    const string ApiLatestRelease = "https://api.github.com/repos/rkhnorkhan-bit/QuadDesk/releases/latest";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<UpdateCheckResult> CheckStableAsync(CancellationToken cancellationToken = default)
    {
        string currentText = CurrentVersionText();
        var current = ParseVersion(currentText);

        using var http = CreateClient();
        using var response = await http.GetAsync(ApiLatestRelease, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub Releases вернул HTTP {(int)response.StatusCode}.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("GitHub Release response is empty.");

        if (release.Draft || release.Prerelease)
            return new(UpdateState.UpToDate, currentText, currentText, release.HtmlUrl, null, null, null, "Последний релиз не является stable-релизом.");

        var latest = ParseVersion(release.TagName);
        string latestText = latest.ToString(3);

        if (latest.CompareTo(current) <= 0)
            return new(UpdateState.UpToDate, currentText, latestText, release.HtmlUrl, null, null, null, "У вас актуальная версия.");

        string expectedSetup = $"QuadDesk-v{latestText}-win-x64-setup.exe";
        var installer = release.Assets.FirstOrDefault(a => string.Equals(a.Name, expectedSetup, StringComparison.OrdinalIgnoreCase))
            ?? release.Assets.FirstOrDefault(a => a.Name.EndsWith("-win-x64-setup.exe", StringComparison.OrdinalIgnoreCase));
        var checksums = release.Assets.FirstOrDefault(a => string.Equals(a.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));

        if (installer is null || string.IsNullOrWhiteSpace(installer.BrowserDownloadUrl))
            throw new InvalidDataException("В релизе нет Windows setup EXE.");
        if (checksums is null || string.IsNullOrWhiteSpace(checksums.BrowserDownloadUrl))
            throw new InvalidDataException("В релизе нет SHA256SUMS.txt.");

        return new(
            UpdateState.Available,
            currentText,
            latestText,
            release.HtmlUrl,
            installer.Name,
            installer.BrowserDownloadUrl,
            checksums.BrowserDownloadUrl,
            $"Доступна версия {latestText}.");
    }

    public static async Task<string> DownloadAndStartStableUpdateAsync(UpdateCheckResult update, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (update.State != UpdateState.Available ||
            string.IsNullOrWhiteSpace(update.InstallerName) ||
            string.IsNullOrWhiteSpace(update.InstallerUrl) ||
            string.IsNullOrWhiteSpace(update.ChecksumsUrl))
            throw new InvalidOperationException("Нет готового stable-обновления.");

        string versionRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuadDesk", "updates", update.LatestVersion);
        string attemptId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8];
        string root = Path.Combine(versionRoot, attemptId);
        Directory.CreateDirectory(root);

        string installerPath = Path.Combine(root, update.InstallerName);
        string checksumsPath = Path.Combine(root, "SHA256SUMS.txt");

        TryPruneOldAttempts(versionRoot, root);

        using var http = CreateClient();

        progress?.Report("Скачиваю SHA256SUMS.txt...");
        await DownloadFileAsync(http, update.ChecksumsUrl, checksumsPath, cancellationToken).ConfigureAwait(false);

        progress?.Report("Скачиваю установщик...");
        await DownloadFileAsync(http, update.InstallerUrl, installerPath, cancellationToken).ConfigureAwait(false);

        progress?.Report("Проверяю SHA-256...");
        string expectedHash = FindExpectedHash(checksumsPath, update.InstallerName);
        string actualHash = await Sha256Async(installerPath, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SHA-256 не совпал. Ожидалось {expectedHash}, получено {actualHash}. Установщик не будет запущен.");

        string updaterSource = Path.Combine(AppContext.BaseDirectory, "QuadDesk.Updater.exe");
        if (!File.Exists(updaterSource))
            throw new FileNotFoundException("QuadDesk.Updater.exe отсутствует рядом с QuadDesk.exe. Обновление невозможно.", updaterSource);

        string updaterCopy = Path.Combine(Path.GetTempPath(), "QuadDesk.Updater-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(updaterSource, updaterCopy, overwrite: true);

        string appExe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "QuadDesk.exe");
        int pid = Environment.ProcessId;

        progress?.Report("Запускаю внешний updater...");
        var args = string.Join(' ',
            "--parent-pid", pid.ToString(),
            "--installer", Quote(installerPath),
            "--app-exe", Quote(appExe),
            "--silent",
            "--launch");

        Process.Start(new ProcessStartInfo
        {
            FileName = updaterCopy,
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory
        });

        return installerPath;
    }

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("QuadDesk", CurrentVersionText().Replace('+', '-')));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    static async Task DownloadFileAsync(HttpClient http, string url, string path, CancellationToken cancellationToken)
    {
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Download failed: HTTP {(int)response.StatusCode}.");

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, path, overwrite: false);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    static void TryPruneOldAttempts(string versionRoot, string currentRoot)
    {
        try
        {
            string current = Path.GetFullPath(currentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var dir in Directory.EnumerateDirectories(versionRoot)
                         .OrderByDescending(Directory.GetCreationTimeUtc)
                         .Skip(4))
            {
                string candidate = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(candidate, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            Log.Write("Updater cleanup skipped: " + ex.GetType().Name);
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Updater temp cleanup skipped: " + ex.GetType().Name);
        }
    }

    static string FindExpectedHash(string checksumsPath, string fileName)
    {
        foreach (string line in File.ReadLines(checksumsPath))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && string.Equals(parts[^1], fileName, StringComparison.OrdinalIgnoreCase))
                return parts[0].ToLowerInvariant();
        }

        throw new InvalidDataException($"SHA256SUMS.txt не содержит запись для {fileName}.");
    }

    static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static Version ParseVersion(string value)
    {
        value = value.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];
        int plus = value.IndexOf('+'); if (plus >= 0) value = value[..plus];
        int dash = value.IndexOf('-'); if (dash >= 0) value = value[..dash];

        if (Version.TryParse(value, out var version))
        {
            int build = version.Build < 0 ? 0 : version.Build;
            return new Version(version.Major, version.Minor, build);
        }

        throw new FormatException("Некорректная версия: " + value);
    }

    static string CurrentVersionText()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null) return "0.0.0";
        int build = version.Build < 0 ? 0 : version.Build;
        return $"{version.Major}.{version.Minor}.{build}";
    }

    static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "0.0.0";
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = [];
    }

    sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
