using QuadDesk.Core;
using Xunit;
namespace QuadDesk.Tests;

public class HotkeyTests
{
    sealed class FakeBackend : IHotkeyBackend
    {
        public readonly Dictionary<int, HotkeyChord> Held = [];
        public readonly HashSet<HotkeyChord> External = [];
        public int Failure;
        public HotkeyChord? ThrowOn;
        public int Register(int id, HotkeyChord chord)
        {
            if (chord == ThrowOn) throw new InvalidOperationException("Injected registration failure");
            if (Failure != 0) return Failure;
            if (External.Contains(chord) || Held.ContainsValue(chord)) return 1409;
            Held.Add(id, chord); return 0;
        }
        public void Unregister(int id) => Held.Remove(id);
    }
    [Theory]
    [InlineData("Ctrl+Alt+1", 3u, 49u)]
    [InlineData("alt + CTRL + D1", 3u, 49u)]
    [InlineData("Ctrl+Alt+Shift+4", 7u, 52u)]
    [InlineData("Win+Alt+Left", 9u, 37u)]
    [InlineData("Ctrl+F9", 2u, 120u)]
    [InlineData("Ctrl+NumPad1", 2u, 97u)]
    [InlineData("Ctrl+PageUp", 2u, 33u)]
    public void ParsesKeys(string text, uint modifiers, uint key)
    { Assert.True(HotkeyChord.TryParse(text, out var chord)); Assert.Equal(new(modifiers, key), chord); }
    [Theory]
    [InlineData("")][InlineData("Ctrl")][InlineData("1")][InlineData("Ctrl++1")]
    [InlineData("Ctrl+Ctrl+1")][InlineData("Ctrl+A+B")][InlineData("Ctrl+F25")]
    [InlineData("Ctrl+ControlKey")][InlineData("Ctrl+Unknown")]
    public void RejectsMalformedBindings(string text) => Assert.False(HotkeyChord.TryParse(text, out _));
    [Fact] public void BasicSetIsUniqueAndContainsNoWindowsKeys()
    {
        var chords = new HashSet<HotkeyChord>();
        foreach (string value in AppConfig.DefaultHotkeys().Values)
        { Assert.True(HotkeyChord.TryParse(value, out var chord)); Assert.Equal(0u, chord.Modifiers & 8); Assert.NotEqual(0x7Bu, chord.Key); Assert.True(chords.Add(chord)); }
        Assert.Equal("Ctrl+Alt+0", AppConfig.DefaultHotkeys()["toggle"]);
        Assert.Equal("Ctrl+Alt+Shift+5", AppConfig.DefaultHotkeys()["layout:main-plus-3"]);
    }
    [Fact] public void RebindReleasesOwnShortcutsBeforeRegistering()
    {
        var backend = new FakeBackend(); using var registry = new HotkeyRegistry(backend);
        registry.Bind(AppConfig.DefaultHotkeys());
        Assert.All(registry.Bind(AppConfig.DefaultHotkeys()), s => Assert.True(s.Registered));
        Assert.Equal(AppConfig.DefaultHotkeys().Count, backend.Held.Count);
    }
    [Fact] public void ProbeRestoresCurrentBindingsAndCommandRouting()
    {
        var backend = new FakeBackend(); using var registry = new HotkeyRegistry(backend);
        registry.Bind(new Dictionary<string, string> { ["toggle"] = "Ctrl+Alt+0" });
        Assert.True(Assert.Single(registry.Probe(new Dictionary<string, string> { ["zone:1"] = "Ctrl+Alt+1" })).Registered);
        Assert.Equal("toggle", registry.ActionFor(1)); Assert.Single(backend.Held);
        Assert.Equal(48u, backend.Held[1].Key); Assert.Equal("toggle", Assert.Single(registry.Statuses).Action);
    }
    [Fact] public void ProbeDoesNotReportOurOwnBindingAsAConflict()
    {
        var backend = new FakeBackend(); using var registry = new HotkeyRegistry(backend);
        var settings = new Dictionary<string, string> { ["toggle"] = "Ctrl+Alt+0" };
        registry.Bind(settings); Assert.True(Assert.Single(registry.Probe(settings)).Registered);
    }
    [Fact] public void ProbeRestoresEvenWhenBackendThrows()
    {
        var backend = new FakeBackend(); using var registry = new HotkeyRegistry(backend);
        registry.Bind(new Dictionary<string, string> { ["toggle"] = "Ctrl+Alt+0" });
        backend.ThrowOn = new(3, 49);
        Assert.Throws<InvalidOperationException>(() => registry.Probe(new Dictionary<string, string> { ["zone:1"] = "Ctrl+Alt+1" }));
        Assert.Equal("toggle", registry.ActionFor(1)); Assert.Equal(48u, Assert.Single(backend.Held).Value.Key);
    }
    [Fact] public void ExternalConflictsHaveRealCodeAndDoNotBreakOtherKeys()
    {
        var backend = new FakeBackend(); backend.External.Add(new(3, 49)); using var registry = new HotkeyRegistry(backend);
        var results = registry.Bind(new Dictionary<string, string> { ["zone:1"] = "Ctrl+Alt+1", ["zone:2"] = "Ctrl+Alt+2" });
        Assert.Equal(1409, results[0].ErrorCode); Assert.False(results[0].Registered); Assert.True(results[1].Registered);
        Assert.Null(registry.ActionFor(1)); Assert.Equal("zone:2", registry.ActionFor(2));
    }
    [Fact] public void OtherWindowsErrorsAreNotMisreportedAsOccupied()
    {
        using var registry = new HotkeyRegistry(new FakeBackend { Failure = 5 });
        var result = Assert.Single(registry.Bind(new Dictionary<string, string> { ["toggle"] = "Ctrl+Alt+0" }));
        Assert.Equal(5, result.ErrorCode); Assert.DoesNotContain("Занята", result.Message);
    }
    [Fact] public void DuplicateAliasesAndReservedKeysAreNotRegistered()
    {
        var backend = new FakeBackend(); using var registry = new HotkeyRegistry(backend);
        var results = registry.Bind(new Dictionary<string, string> { ["zone:1"] = "Ctrl+Alt+1", ["zone:2"] = "Alt+Ctrl+D1", ["toggle"] = "Ctrl+F12", ["restore"] = "" });
        Assert.True(results[0].Registered); Assert.All(results.Skip(1), s => Assert.False(s.Registered)); Assert.Single(backend.Held);
        Assert.Contains("Повторяется", results[1].Message); Assert.Contains("F12", results[2].Message); Assert.Equal("Отключено", results[3].Message);
    }
    [Fact] public void SuspensionRebindAndDisposeReleaseAllRegistrations()
    {
        var backend = new FakeBackend(); var registry = new HotkeyRegistry(backend);
        registry.Bind(AppConfig.DefaultHotkeys()); registry.Clear(); Assert.Empty(backend.Held); Assert.Null(registry.ActionFor(1));
        registry.Rebind(); Assert.NotEmpty(backend.Held); registry.Dispose(); Assert.Empty(backend.Held);
    }
}
