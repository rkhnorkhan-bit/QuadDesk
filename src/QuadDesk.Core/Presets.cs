namespace QuadDesk.Core;
public static class Presets
{
    static ZoneNode Z(string id, string? name = null) => new() { Id = id, Name = name ?? id };
    static SplitNode V(LayoutNode a, LayoutNode b, double ratio = .5) => new() { Orientation = SplitOrientation.Vertical, Ratio = ratio, First = a, Second = b };
    static SplitNode H(LayoutNode a, LayoutNode b, double ratio = .5) => new() { Orientation = SplitOrientation.Horizontal, Ratio = ratio, First = a, Second = b };
    static LayoutDefinition L(string id, string name, LayoutNode root) => new() { Id = id, Name = name, Root = root };
    public static List<LayoutDefinition> All() => [
        L("single", "Single", Z("main", "Main")),
        L("dual-vertical", "Dual Vertical", V(Z("main", "Main"), Z("right", "Right"))),
        L("dual-horizontal", "Dual Horizontal", H(Z("top", "Top"), Z("bottom", "Bottom"))),
        L("triple-columns", "Triple Columns", V(Z("left", "Left"), V(Z("main", "Main"), Z("right", "Right")), 1d/3)),
        L("triple-main", "Triple Main", V(Z("main", "Main"), H(Z("top-right", "Top Right"), Z("bottom-right", "Bottom Right")), .65)),
        L("quad", "Quad 2×2", H(V(Z("top-left", "Top Left"), Z("top-right", "Top Right")), V(Z("bottom-left", "Bottom Left"), Z("bottom-right", "Bottom Right")))),
        L("main-plus-3", "Main + 3", V(Z("main", "Main"), H(Z("top-right", "Top Right"), H(Z("middle-right", "Middle Right"), Z("bottom-right", "Bottom Right")), 1d/3), .65)),
        L("six", "Six 3×2", H(V(Z("top-left"), V(Z("top-center"), Z("top-right")), 1d/3), V(Z("bottom-left"), V(Z("bottom-center"), Z("bottom-right")), 1d/3))) ];
}
