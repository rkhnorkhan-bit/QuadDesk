using System.Text.Json;
using QuadDesk.Core;
using QuadDesk.Services;
using Xunit;
namespace QuadDesk.Tests;
public class LayoutTests
{
    static LayoutDefinition Preset(string id) => Presets.All().Single(l => l.Id == id);
    [Theory]
    [InlineData(0, 0)] [InlineData(2560, 0)] [InlineData(-3840, 0)] [InlineData(0, -2160)]
    public void QuadPhysicalCoordinates(int x, int y)
    {
        var zones = LayoutEngine.Calculate(Preset("quad"), new(x, y, 3840, 2160));
        Assert.Equal(new PixelRect(x, y, 1920, 1080), zones[0].Bounds);
        Assert.Equal(new PixelRect(x + 1920, y, 1920, 1080), zones[1].Bounds);
        Assert.Equal(new PixelRect(x, y + 1080, 1920, 1080), zones[2].Bounds);
        Assert.Equal(new PixelRect(x + 1920, y + 1080, 1920, 1080), zones[3].Bounds);
    }
    [Fact] public void MainThreeExact()
    {
        var z = LayoutEngine.Calculate(Preset("main-plus-3"), new(0, 0, 3840, 2160));
        Assert.Equal(new PixelRect(0, 0, 2496, 2160), z[0].Bounds);
        for (int i = 1; i <= 3; i++) Assert.Equal(new PixelRect(2496, (i - 1) * 720, 1344, 720), z[i].Bounds);
    }
    [Theory] [InlineData(3840, 2160)] [InlineData(3839, 2159)] [InlineData(1920, 1080)] [InlineData(2160, 3840)] [InlineData(1001, 777)]
    public void EveryPresetPartitionsExactly(int w, int h)
    {
        var area = new PixelRect(-3900, -200, w, h);
        foreach (var layout in Presets.All()) Cover(LayoutEngine.Calculate(layout, area), area);
    }
    static void Cover(List<Zone> zones, PixelRect area)
    {
        Assert.Equal(area.Area, zones.Sum(z => z.Bounds.Area));
        for (int i = 0; i < zones.Count; i++)
        {
            var a = zones[i].Bounds; Assert.True(a.Width > 0 && a.Height > 0); Assert.Equal(a.Area, a.Overlap(area));
            for (int j = i + 1; j < zones.Count; j++) Assert.Equal(0, a.Overlap(zones[j].Bounds));
        }
    }
    [Fact] public void RandomNestedTreesCoverWithoutGaps()
    {
        var rng = new Random(250922);
        LayoutNode Node(int depth) => depth == 0 ? new ZoneNode() : new SplitNode { Orientation = rng.Next(2) == 0 ? SplitOrientation.Vertical : SplitOrientation.Horizontal, Ratio = .3 + rng.NextDouble() * .4, First = Node(depth - 1), Second = Node(depth - 1) };
        for (int i = 0; i < 100; i++)
        { var layout = new LayoutDefinition { Root = Node(4) }; var area = new PixelRect(rng.Next(-6000, 6000), rng.Next(-4000, 4000), rng.Next(3000, 5000), rng.Next(2000, 4000)); Cover(LayoutEngine.Calculate(layout, area), area); }
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(-.5)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidRatioRejected(double ratio)
    { var l = Preset("dual-vertical"); ((SplitNode)l.Root).Ratio = ratio; Assert.Throws<ArgumentException>(() => LayoutEngine.Validate(l)); }
    [Fact] public void DuplicateIdsRejected()
    { var l = Preset("dual-vertical"); ((ZoneNode)((SplitNode)l.Root).Second).Id = "main"; Assert.Throws<ArgumentException>(() => LayoutEngine.Validate(l)); }
    [Fact] public void CycleRejected()
    { var l = Preset("dual-vertical"); ((SplitNode)l.Root).First = l.Root; Assert.Throws<ArgumentException>(() => LayoutEngine.Validate(l)); }
    [Fact] public void MinimumDimensionsEnforced()
    { Assert.Throws<ArgumentException>(() => LayoutEngine.Calculate(Preset("six"), new(0, 0, 500, 400), 200, 150)); }
    [Fact] public void PixelUnderflowRejected()
    { Assert.Throws<ArgumentException>(() => LayoutEngine.Calculate(Preset("dual-vertical"), new(0, 0, 1, 100))); }
    [Theory]
    [InlineData("quad", "top-left", Direction.Right, "top-right")]
    [InlineData("quad", "bottom-right", Direction.Up, "top-right")]
    [InlineData("quad", "top-right", Direction.Left, "top-left")]
    [InlineData("quad", "top-left", Direction.Down, "bottom-left")]
    [InlineData("main-plus-3", "main", Direction.Right, "middle-right")]
    [InlineData("main-plus-3", "top-right", Direction.Down, "middle-right")]
    [InlineData("main-plus-3", "bottom-right", Direction.Left, "main")]
    [InlineData("triple-columns", "main", Direction.Right, "right")]
    [InlineData("triple-columns", "main", Direction.Left, "left")]
    [InlineData("six", "top-center", Direction.Down, "bottom-center")]
    public void GeometricNavigation(string layout, string from, Direction dir, string expected)
    { var z = LayoutEngine.Calculate(Preset(layout), new(2560, -1000, 3840, 2160)); Assert.Equal(expected, LayoutEngine.Neighbor(z, z.Single(x => x.Id == from), dir)?.Id); }
    [Fact] public void NoWrapAtEdge()
    { var z = LayoutEngine.Calculate(Preset("quad"), new(0, 0, 3840, 2160)); Assert.Null(LayoutEngine.Neighbor(z, z[0], Direction.Left)); }

    [Fact] public void SmartSnapUsesParentZoneAsItsOwnWorkspace()
    {
        var zone = new PixelRect(1920, 1080, 1921, 1081);
        Assert.Equal(new PixelRect(1920, 1080, 960, 1081), LayoutEngine.LocalSnapRect(zone, LocalSnapSlot.Left));
        Assert.Equal(new PixelRect(2880, 1080, 961, 1081), LayoutEngine.LocalSnapRect(zone, LocalSnapSlot.Right));
        Assert.Equal(new PixelRect(1920, 1080, 960, 540), LayoutEngine.LocalSnapRect(zone, LocalSnapSlot.TopLeft));
        Assert.Equal(new PixelRect(2880, 1620, 961, 541), LayoutEngine.LocalSnapRect(zone, LocalSnapSlot.BottomRight));
        Assert.Equal(zone.Area, LayoutEngine.LocalSnapRect(zone, LocalSnapSlot.Left).Area + LayoutEngine.LocalSnapRect(zone, LocalSnapSlot.Right).Area);
    }

    [Theory]
    [InlineData(100, 400, LocalSnapSlot.Left)]
    [InlineData(899, 400, LocalSnapSlot.Right)]
    [InlineData(101, 101, LocalSnapSlot.TopLeft)]
    [InlineData(898, 101, LocalSnapSlot.TopRight)]
    [InlineData(101, 699, LocalSnapSlot.BottomLeft)]
    [InlineData(898, 699, LocalSnapSlot.BottomRight)]
    [InlineData(500, 101, LocalSnapSlot.Full)]
    [InlineData(500, 400, LocalSnapSlot.Free)]
    public void SmartSnapDetectsLocalEdges(int x, int y, LocalSnapSlot expected)
    {
        var zone = new PixelRect(100, 100, 800, 600);
        Assert.Equal(expected, LayoutEngine.DetectLocalSnapSlot(zone, x, y));
    }

    [Fact] public void NativeMonitorSnapCanBeTranslatedIntoAParentZone()
    {
        var monitor = new PixelRect(0, 0, 3840, 2160);
        Assert.Equal(LocalSnapSlot.Left, LayoutEngine.MatchSnapRect(monitor, new(0, 0, 1920, 2160)));
        Assert.Equal(LocalSnapSlot.BottomRight, LayoutEngine.MatchSnapRect(monitor, new(1920, 1080, 1920, 1080)));
        Assert.Equal(LocalSnapSlot.Free, LayoutEngine.MatchSnapRect(monitor, new(200, 200, 1200, 800)));
    }

    [Fact] public void SmartSnapContainmentHandlesNegativeMonitorCoordinates()
    {
        var zone = new PixelRect(-3840, -1080, 1920, 1080);
        Assert.True(LayoutEngine.IsInside(new(-3800, -1000, 900, 600), zone));
        Assert.False(LayoutEngine.IsInside(new(-3900, -1000, 900, 600), zone));
        Assert.Equal(new PixelRect(-3840, -1000, 900, 600), new PixelRect(-3900, -1000, 900, 600).FitInside(zone));
    }
    [Fact] public void PerpendicularOverlapTakesPriority()
    {
        var from = new Zone("from", "", new(0, 0, 100, 100));
        var diagonal = new Zone("diagonal", "", new(101, 101, 10, 10)); var aligned = new Zone("aligned", "", new(500, 0, 100, 100));
        Assert.Equal("aligned", LayoutEngine.Neighbor([from, diagonal, aligned], from, Direction.Right)?.Id);
    }
    [Fact] public void TinyOverlapDoesNotAssign()
    { var z = LayoutEngine.Calculate(Preset("single"), new(0, 0, 100, 100)); Assert.Null(LayoutEngine.Detect(z, new(99, 99, 500, 500))); }
    [Fact] public void SplitPreservesStableIdAndRemoveCollapses()
    {
        var l = Preset("single"); l.Root = LayoutEngine.Replace(l.Root, "main", z => new SplitNode { First = z, Second = new ZoneNode { Id = "new" } });
        Assert.Equal(new[] { "main", "new" }, LayoutEngine.Calculate(l, new(0, 0, 1000, 1000)).Select(z => z.Id));
        l.Root = LayoutEngine.Remove(l.Root, "new"); Assert.IsType<ZoneNode>(l.Root); Assert.Equal("main", ((ZoneNode)l.Root).Id);
    }
    [Fact] public void LayoutRoundtrip()
    {
        foreach (var l in Presets.All()) { var clone = JsonData.Clone(l); Assert.Equal(JsonSerializer.Serialize(l, JsonData.Options), JsonSerializer.Serialize(clone, JsonData.Options)); Cover(LayoutEngine.Calculate(clone, new(-3840, 0, 3840, 2160)), new(-3840, 0, 3840, 2160)); }
    }
    [Fact] public void ConfigAndWorkspaceRoundtrip()
    {
        var c = new AppConfig { TargetDevicePath = @"\\?\DISPLAY#HAIER", SmartSnap = true, Rules = [new() { ProcessName = "Code.exe", ZoneId = "main" }] };
        var configClone = JsonData.Clone(c); Assert.Equal(c.TargetDevicePath, configClone.TargetDevicePath); Assert.Equal(c.Hotkeys, configClone.Hotkeys); Assert.True(configClone.SmartSnap);
        var w = new Workspace { LayoutId = "main-plus-3", Windows = [new() { Identity = new("Code", "C:/Code.exe", "Chrome_WidgetWin_1"), ZoneId = "main", X = .1, Width = .8, LogicalMaximized = false, SnapSlot = LocalSnapSlot.Left }] };
        var clone = JsonData.Clone(w); Assert.Equal(w.Windows[0].Identity, clone.Windows[0].Identity); Assert.False(clone.Windows[0].LogicalMaximized); Assert.Equal(.8, clone.Windows[0].Width); Assert.Equal(LocalSnapSlot.Left, clone.Windows[0].SnapSlot);
    }
    [Fact] public void BadDiscriminatorRejected()
    { Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LayoutNode>("{\"type\":\"unknown\"}", JsonData.Options)); }
    [Fact] public void AtomicSaveLeavesReadableJson()
    {
        string dir = Path.Combine(Path.GetTempPath(), "quaddesk-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { string file = Path.Combine(dir, "config.json"); Storage.Save(file, new AppConfig()); Storage.Save(file, new AppConfig { Enabled = false }); Assert.False(Storage.Read<AppConfig>(file).Enabled); Assert.Single(Directory.GetFiles(dir)); }
        finally { Directory.Delete(dir, true); }
    }
    [Theory] [InlineData("../escape")] [InlineData("a/b")] [InlineData("a\\b")] [InlineData("")]
    public void UnsafeLayoutFileNameRejected(string id) => Assert.Throws<InvalidDataException>(() => Storage.SafeId(id));
    [Fact] public void NullCollectionsRejected()
    { Assert.Throws<InvalidDataException>(() => Storage.ValidateConfig(new AppConfig { Rules = null! })); }
    [Theory] [InlineData("{broken")] [InlineData("null")] public void CorruptConfigQuarantinedWithNoTarget(string badJson)
    {
        string dir = Path.Combine(Path.GetTempPath(), "quaddesk-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Storage(dir); File.WriteAllText(store.ConfigPath, badJson);
            var config = store.LoadConfig(); Assert.Null(config.TargetDevicePath);
            Assert.Single(Directory.GetFiles(dir, "config.json.bad.*")); Assert.Single(store.Warnings);
        }
        finally { Directory.Delete(dir, true); }
    }
    [Fact] public void CorruptLayoutDoesNotLoseOthers()
    {
        string dir = Path.Combine(Path.GetTempPath(), "quaddesk-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Storage(dir); Assert.Equal(8, store.LoadLayouts().Count);
            File.WriteAllText(store.LayoutPath("main-plus-3"), "null");
            var layouts = store.LoadLayouts(); Assert.Equal(7, layouts.Count);
            Assert.DoesNotContain(layouts, l => l.Id == "main-plus-3");
            Assert.Single(Directory.GetFiles(Path.Combine(dir, "layouts"), "main-plus-3.json.bad.*"));
        }
        finally { Directory.Delete(dir, true); }
    }
    [Fact] public void DeletedPresetNotReseeded()
    {
        string dir = Path.Combine(Path.GetTempPath(), "quaddesk-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Storage(dir); var layouts = store.LoadLayouts(); store.DeleteLayout(layouts.Single(l => l.Id == "quad"));
            Assert.DoesNotContain(store.LoadLayouts(), l => l.Id == "quad");
        }
        finally { Directory.Delete(dir, true); }
    }
    [Fact] public void NormalRectFitsNegativeCoordinates()
    { Assert.Equal(new PixelRect(-3840, -2160, 1000, 800), new PixelRect(-6000, -5000, 1000, 800).FitInside(new(-3840, -2160, 1920, 1080))); }
}
public class RuleTests
{
    static WindowRule Rule => new() { ProcessName = "Code.exe", LayoutId = "coding", ZoneId = "main", WindowClass = "Editor", TitleContains = "workspace" };
    [Theory]
    [InlineData("code", "Editor", "My Workspace", "coding", false, true)]
    [InlineData("Code.exe", "editor", "WORKSPACE", "coding", false, true)]
    [InlineData("other", "Editor", "workspace", "coding", false, false)]
    [InlineData("Code", "Dialog", "workspace", "coding", false, false)]
    [InlineData("Code", "Editor", "other", "coding", false, false)]
    [InlineData("Code", "Editor", "workspace", "other", false, false)]
    [InlineData("Code", "Editor", "workspace", "coding", true, false)]
    public void MatchIdentityAndManualOverride(string process, string cls, string title, string layout, bool manual, bool matches)
    { Assert.Equal(matches, RuleMatcher.Match([Rule], new(process, "", cls), title, layout, manual) is not null); }
    [Fact] public void StableZoneReference()
    { var r = Rule; Assert.Equal("main", RuleMatcher.Match([r], new("Code", "", "Editor"), "workspace", "coding", false)?.ZoneId); }
}
