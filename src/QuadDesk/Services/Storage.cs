using System.Text.Json;
using QuadDesk.Core;
namespace QuadDesk.Services;

internal static class Log
{
    public static readonly string Root = AppContext.BaseDirectory;
    public static void Write(string message)
    {
        try
        {
            var dir = Path.Combine(Root, "logs"); Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "quad-desk.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024) File.Move(path, path + ".1", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
internal sealed class Storage
{
    public string Root { get; }
    public string ConfigPath => Path.Combine(Root, "config.json");
    public List<string> Warnings { get; } = [];
    public Storage(string? root = null)
    {
        Root = root ?? AppContext.BaseDirectory;
        foreach (var dir in new[] { "layouts", "workspaces", "logs" }) Directory.CreateDirectory(Path.Combine(Root, dir));
        var probe = Path.Combine(Root, ".write-test-" + Guid.NewGuid().ToString("N")); File.WriteAllText(probe, ""); File.Delete(probe);
    }
    public static void Save<T>(string path, T data)
    {
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, data, JsonData.Options); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static T Read<T>(string path)
    {
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("JSON больше 4 MB.");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonData.Options) ?? throw new InvalidDataException("Пустой JSON.");
    }
    public static void ValidateConfig(AppConfig c)
    {
        if (c.SchemaVersion != 1 || c.MinimumZoneWidth < 40 || c.MinimumZoneWidth > 4000 || c.MinimumZoneHeight < 40 || c.MinimumZoneHeight > 4000 || c.GuardIntervalMs < 50 || c.GuardIntervalMs > 1000 || c.Rules is null || c.Hotkeys is null || c.ExcludedProcesses is null || string.IsNullOrWhiteSpace(c.ActiveLayoutId)) throw new InvalidDataException("Некорректные настройки.");
        if (c.Rules.Any(r => r is null || string.IsNullOrWhiteSpace(r.ProcessName) || string.IsNullOrWhiteSpace(r.LayoutId) || string.IsNullOrWhiteSpace(r.ZoneId)) || c.ExcludedProcesses.Any(p => p is null) || c.Hotkeys.Any(h => string.IsNullOrWhiteSpace(h.Key) || h.Value is null)) throw new InvalidDataException("Некорректные правила или клавиши.");
    }
    public AppConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath)) return new();
        try { var c = Read<AppConfig>(ConfigPath); ValidateConfig(c); return c; }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or ArgumentException or NotSupportedException)
        { Quarantine(ConfigPath); Warnings.Add("Повреждённый config сохранён как .bad; загружены безопасные настройки без выбранного монитора."); return new(); }
    }
    public List<LayoutDefinition> LoadLayouts()
    {
        string dir = Path.Combine(Root, "layouts");
        // Deleted presets remain deleted: seed only a pristine directory.
        if (!File.Exists(Path.Combine(dir, ".initialized")))
        { foreach (var l in Presets.All()) if (!File.Exists(LayoutPath(l.Id))) SaveLayout(l); File.WriteAllText(Path.Combine(dir, ".initialized"), "1"); }
        var result = new List<LayoutDefinition>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var l = Read<LayoutDefinition>(file); LayoutEngine.Validate(l);
                if (Path.GetFileNameWithoutExtension(file) != SafeId(l.Id) || result.Any(x => x.Id == l.Id)) throw new InvalidDataException("ID файла не совпадает.");
                result.Add(l);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or ArgumentException or NotSupportedException)
            { Quarantine(file); Warnings.Add("Некорректная раскладка отключена: " + Path.GetFileName(file)); }
        }
        if (result.Count == 0) { var l = Presets.All()[0]; SaveLayout(l); result.Add(l); }
        return result;
    }
    public static string SafeId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 100 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) throw new InvalidDataException("ID: только A–Z, 0–9, дефис и подчёркивание.");
        return id;
    }
    public string LayoutPath(string id) => Path.Combine(Root, "layouts", SafeId(id) + ".json");
    public void SaveLayout(LayoutDefinition l) { LayoutEngine.Validate(l); l.UpdatedAt = DateTimeOffset.UtcNow; Save(LayoutPath(l.Id), l); }
    public void DeleteLayout(LayoutDefinition l) => File.Delete(LayoutPath(l.Id));
    void Quarantine(string path) { File.Move(path, path + ".bad." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")); Log.Write("Invalid JSON quarantined"); }
}
