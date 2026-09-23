namespace QuadDesk.Core;

public readonly record struct HotkeyChord(uint Modifiers, uint Key)
{
    static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
        ["Space"] = 0x20, ["Tab"] = 9, ["Enter"] = 13, ["Return"] = 13,
        ["Escape"] = 27, ["Esc"] = 27, ["Back"] = 8, ["Backspace"] = 8,
        ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["Prior"] = 0x21,
        ["PageDown"] = 0x22, ["Next"] = 0x22, ["Insert"] = 0x2D, ["Delete"] = 0x2E,
        ["Multiply"] = 0x6A, ["Add"] = 0x6B, ["Subtract"] = 0x6D,
        ["Decimal"] = 0x6E, ["Divide"] = 0x6F
    };
    public static bool TryParse(string? text, out HotkeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        uint mods = 0, key = 0;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries))
        {
            uint modifier = part.ToLowerInvariant() switch { "ctrl" => 2, "alt" => 1, "shift" => 4, "win" => 8, _ => 0 };
            if (modifier != 0)
            { if ((mods & modifier) != 0) return false; mods |= modifier; continue; }
            if (key != 0 || part.Length == 0) return false;
            if (part.Length == 1 && char.IsAsciiLetterOrDigit(part[0])) key = char.ToUpperInvariant(part[0]);
            else if (NamedKeys.TryGetValue(part, out uint named)) key = named;
            else if (part.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) && part.Length == 7 && part[6] is >= '0' and <= '9') key = 0x60u + (uint)(part[6] - '0');
            else if (part.Length == 2 && part[0] is 'D' or 'd' && part[1] is >= '0' and <= '9') key = part[1];
            else if (part.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(part[1..], out int f) && f is >= 1 and <= 24) key = (uint)(0x70 + f - 1);
            else return false;
        }
        if (mods == 0 || key == 0) return false;
        chord = new(mods, key); return true;
    }
}

public sealed record HotkeyStatus(string Action, string Binding, bool Registered, string Message, int ErrorCode = 0);

// The backend is native on Windows and replaceable in tests.
public interface IHotkeyBackend
{
    int Register(int id, HotkeyChord chord); // 0 = success, otherwise GetLastError
    void Unregister(int id);
}

public sealed class HotkeyRegistry(IHotkeyBackend backend) : IDisposable
{
    readonly Dictionary<int, string> commands = [];
    Dictionary<string, string> configured = [];
    public IReadOnlyList<HotkeyStatus> Statuses { get; private set; } = [];
    public string? ActionFor(int id) => commands.GetValueOrDefault(id);
    public IReadOnlyList<HotkeyStatus> Bind(IReadOnlyDictionary<string, string> bindings)
    {
        Clear(); configured = bindings.ToDictionary(x => x.Key, x => x.Value);
        var statuses = new List<HotkeyStatus>(); var seen = new HashSet<HotkeyChord>(); int id = 0;
        foreach (var (action, binding) in configured)
        {
            if (string.IsNullOrWhiteSpace(binding)) { statuses.Add(new(action, binding, false, "Отключено")); continue; }
            if (!HotkeyChord.TryParse(binding, out var chord)) { statuses.Add(new(action, binding, false, "Неверный формат")); continue; }
            if (chord.Key == 0x7B) { statuses.Add(new(action, binding, false, "F12 зарезервирована отладчиком Windows")); continue; }
            if (!seen.Add(chord)) { statuses.Add(new(action, binding, false, "Повторяется в QuadDesk")); continue; }
            int next = ++id, error = backend.Register(next, chord);
            if (error == 0) { commands[next] = action; statuses.Add(new(action, binding, true, "Работает")); }
            else statuses.Add(new(action, binding, false, error == 1409 ? "Занята другой программой или Windows (1409)" : $"Windows отклонила регистрацию: код {error}", error));
        }
        return Statuses = statuses;
    }
    public IReadOnlyList<HotkeyStatus> Probe(IReadOnlyDictionary<string, string> bindings)
    {
        var previous = configured.ToDictionary(x => x.Key, x => x.Value);
        try { return Bind(bindings).Select(s => s.Registered ? s with { Message = "Доступно при проверке" } : s).ToArray(); }
        finally { Bind(previous); }
    }
    public void Clear()
    { foreach (int id in commands.Keys) backend.Unregister(id); commands.Clear(); }
    public void Rebind() => Bind(configured);
    public void Dispose() => Clear();
}
