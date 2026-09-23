using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuadDesk.Core;

public enum SplitOrientation { Vertical, Horizontal }
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SplitNode), "split")]
[JsonDerivedType(typeof(ZoneNode), "zone")]
public abstract class LayoutNode { }
public sealed class SplitNode : LayoutNode
{
    public SplitOrientation Orientation { get; set; }
    public double Ratio { get; set; } = .5;
    public LayoutNode First { get; set; } = new ZoneNode();
    public LayoutNode Second { get; set; } = new ZoneNode();
}
public sealed class ZoneNode : LayoutNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Зона";
}
public sealed class LayoutDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Новая раскладка";
    public LayoutNode Root { get; set; } = new ZoneNode { Id = "main", Name = "Main" };
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public override string ToString() => Name;
}
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    [JsonIgnore] public int Right => checked(X + Width);
    [JsonIgnore] public int Bottom => checked(Y + Height);
    [JsonIgnore] public double CenterX => X + Width / 2d;
    [JsonIgnore] public double CenterY => Y + Height / 2d;
    [JsonIgnore] public long Area => (long)Width * Height;
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
    public long Overlap(PixelRect b) => (long)Math.Max(0, Math.Min(Right, b.Right) - Math.Max(X, b.X)) * Math.Max(0, Math.Min(Bottom, b.Bottom) - Math.Max(Y, b.Y));
    public PixelRect FitInside(PixelRect b) { int w = Math.Clamp(Width, 1, b.Width), h = Math.Clamp(Height, 1, b.Height); return new(Math.Clamp(X, b.X, b.Right - w), Math.Clamp(Y, b.Y, b.Bottom - h), w, h); }
}
public sealed record Zone(string Id, string Name, PixelRect Bounds);
public enum Direction { Left, Right, Up, Down }
public sealed class WindowRule
{
    public string ProcessName { get; set; } = "";
    public string? WindowClass { get; set; }
    public string? TitleContains { get; set; }
    public string LayoutId { get; set; } = "main-plus-3";
    public string ZoneId { get; set; } = "main";
}
public sealed record WindowIdentity(string ProcessName, string ExecutablePath, string WindowClass);
public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string? TargetDevicePath { get; set; }
    public string ActiveLayoutId { get; set; } = "main-plus-3";
    public bool AutoSnap { get; set; } = true;
    public bool UseFullMonitorBounds { get; set; } = true;
    public bool RestoreWorkspaceOnStart { get; set; }
    public int MinimumZoneWidth { get; set; } = 200;
    public int MinimumZoneHeight { get; set; } = 150;
    public List<string> ExcludedProcesses { get; set; } = [];
    public List<WindowRule> Rules { get; set; } = [];
    public Dictionary<string, string> Hotkeys { get; set; } = DefaultHotkeys();
    public static Dictionary<string, string> DefaultHotkeys()
    {
        var h = new Dictionary<string, string> { ["toggle"] = "Ctrl+Alt+0", ["restore"] = "Ctrl+Alt+R", ["maximize"] = "Ctrl+Alt+M",
            ["left"] = "Ctrl+Alt+Left", ["right"] = "Ctrl+Alt+Right", ["up"] = "Ctrl+Alt+Up", ["down"] = "Ctrl+Alt+Down",
            ["layout:single"] = "Ctrl+Alt+Shift+1", ["layout:dual-vertical"] = "Ctrl+Alt+Shift+2", ["layout:triple-main"] = "Ctrl+Alt+Shift+3", ["layout:quad"] = "Ctrl+Alt+Shift+4", ["layout:six"] = "Ctrl+Alt+Shift+6",
            ["layout:main-plus-3"] = "Ctrl+Alt+Shift+5" };
        for (int i = 1; i <= 9; i++) h[$"zone:{i}"] = $"Ctrl+Alt+{i}";
        return h;
    }
}
public sealed class Workspace
{
    public string LayoutId { get; set; } = "";
    public List<WorkspaceWindow> Windows { get; set; } = [];
}
public sealed class WorkspaceWindow
{
    public WindowIdentity Identity { get; set; } = new("", "", "");
    public int Occurrence { get; set; }
    public string ZoneId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 1;
    public double Height { get; set; } = 1;
    public bool LogicalMaximized { get; set; }
}
public static class JsonData
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 96, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
}
