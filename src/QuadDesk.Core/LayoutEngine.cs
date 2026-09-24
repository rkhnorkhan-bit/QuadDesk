namespace QuadDesk.Core;

public static class LayoutEngine
{
    public static void Validate(LayoutDefinition layout)
    {
        if (string.IsNullOrWhiteSpace(layout.Id) || string.IsNullOrWhiteSpace(layout.Name))
            throw new ArgumentException("У раскладки нет ID или имени.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var nodes = new HashSet<LayoutNode>(ReferenceEqualityComparer.Instance);

        void Walk(LayoutNode? n, int depth)
        {
            if (n is null || depth > 32 || !nodes.Add(n))
                throw new ArgumentException("Некорректное дерево или глубина больше 32.");

            if (n is ZoneNode z)
            {
                if (string.IsNullOrWhiteSpace(z.Id) || string.IsNullOrWhiteSpace(z.Name) || !ids.Add(z.Id))
                    throw new ArgumentException("Пустой или повторный ZoneId/имя.");
                if (ids.Count > 256)
                    throw new ArgumentException("Максимум 256 зон.");
            }
            else if (n is SplitNode s)
            {
                if (!double.IsFinite(s.Ratio) || s.Ratio <= 0 || s.Ratio >= 1 || !Enum.IsDefined(s.Orientation))
                    throw new ArgumentException("Ratio должен быть между 0 и 1.");
                Walk(s.First, depth + 1);
                Walk(s.Second, depth + 1);
            }
            else
            {
                throw new ArgumentException("Неизвестный узел.");
            }
        }

        Walk(layout.Root, 0);
    }

    public static List<Zone> Calculate(LayoutDefinition layout, PixelRect bounds, int minW = 1, int minH = 1)
    {
        Validate(layout);
        if (bounds.Width <= 0 || bounds.Height <= 0 || minW < 1 || minH < 1)
            throw new ArgumentException("Неверные размеры.");

        var zones = new List<Zone>();

        void Walk(LayoutNode node, PixelRect r)
        {
            if (node is ZoneNode z)
            {
                if (r.Width < minW || r.Height < minH)
                    throw new ArgumentException($"Зона {z.Name} меньше {minW}×{minH} px.");
                zones.Add(new(z.Id, z.Name, r));
                return;
            }

            var s = (SplitNode)node;
            var (a, b) = Divide(r, s);
            Walk(s.First, a);
            Walk(s.Second, b);
        }

        Walk(layout.Root, bounds);
        return zones;
    }

    public static (PixelRect First, PixelRect Second) Divide(PixelRect r, SplitNode s)
    {
        bool vertical = s.Orientation == SplitOrientation.Vertical;
        int size = vertical ? r.Width : r.Height;
        int cut = (int)Math.Round(size * s.Ratio, MidpointRounding.AwayFromZero);

        if (cut <= 0 || cut >= size)
            throw new ArgumentException("Разделение создаёт пустую зону.");

        return vertical
            ? (new(r.X, r.Y, cut, r.Height), new(r.X + cut, r.Y, r.Width - cut, r.Height))
            : (new(r.X, r.Y, r.Width, cut), new(r.X, r.Y + cut, r.Width, r.Height - cut));
    }

    public static Zone? Detect(IEnumerable<Zone> zones, PixelRect window, double minimumOverlap = .2)
    {
        var best = zones.OrderByDescending(z => z.Bounds.Overlap(window)).FirstOrDefault();
        return best is not null &&
               window.Area > 0 &&
               best.Bounds.Overlap(window) >= window.Area * minimumOverlap
            ? best
            : null;
    }

    public static Zone? Neighbor(IReadOnlyList<Zone> zones, Zone from, Direction direction)
    {
        bool horizontal = direction is Direction.Left or Direction.Right;
        bool positive = direction is Direction.Right or Direction.Down;

        return zones
            .Where(z => z.Id != from.Id)
            .Where(z =>
                ((horizontal ? z.Bounds.CenterX - from.Bounds.CenterX : z.Bounds.CenterY - from.Bounds.CenterY) *
                 (positive ? 1 : -1)) > 0)
            .OrderByDescending(z =>
                horizontal
                    ? Math.Min(z.Bounds.Bottom, from.Bounds.Bottom) > Math.Max(z.Bounds.Y, from.Bounds.Y)
                    : Math.Min(z.Bounds.Right, from.Bounds.Right) > Math.Max(z.Bounds.X, from.Bounds.X))
            .ThenBy(z =>
                Math.Pow(z.Bounds.CenterX - from.Bounds.CenterX, 2) +
                Math.Pow(z.Bounds.CenterY - from.Bounds.CenterY, 2))
            .ThenBy(z => z.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static PixelRect LocalSnapRect(PixelRect zone, LocalSnapSlot slot)
    {
        if (zone.Width < 2 || zone.Height < 2)
            throw new ArgumentException("Зона слишком мала для Smart Snap.");

        int leftWidth = zone.Width / 2;
        int rightWidth = zone.Width - leftWidth;
        int topHeight = zone.Height / 2;
        int bottomHeight = zone.Height - topHeight;

        return slot switch
        {
            LocalSnapSlot.Full => zone,
            LocalSnapSlot.Left => new(zone.X, zone.Y, leftWidth, zone.Height),
            LocalSnapSlot.Right => new(zone.X + leftWidth, zone.Y, rightWidth, zone.Height),
            LocalSnapSlot.TopLeft => new(zone.X, zone.Y, leftWidth, topHeight),
            LocalSnapSlot.TopRight => new(zone.X + leftWidth, zone.Y, rightWidth, topHeight),
            LocalSnapSlot.BottomLeft => new(zone.X, zone.Y + topHeight, leftWidth, bottomHeight),
            LocalSnapSlot.BottomRight => new(zone.X + leftWidth, zone.Y + topHeight, rightWidth, bottomHeight),
            _ => throw new ArgumentException("Для свободного окна нет фиксированного Smart Snap прямоугольника.")
        };
    }

    public static LocalSnapSlot DetectLocalSnapSlot(PixelRect zone, int x, int y, int threshold = 48)
    {
        if (!zone.Contains(x, y))
            return LocalSnapSlot.Free;

        int maxThreshold = Math.Max(8, Math.Min(zone.Width, zone.Height) / 3);
        threshold = Math.Clamp(threshold, 8, maxThreshold);

        bool left = x - zone.X < threshold;
        bool right = zone.Right - 1 - x < threshold;
        bool top = y - zone.Y < threshold;
        bool bottom = zone.Bottom - 1 - y < threshold;

        if (left && top) return LocalSnapSlot.TopLeft;
        if (right && top) return LocalSnapSlot.TopRight;
        if (left && bottom) return LocalSnapSlot.BottomLeft;
        if (right && bottom) return LocalSnapSlot.BottomRight;
        if (left) return LocalSnapSlot.Left;
        if (right) return LocalSnapSlot.Right;
        if (top) return LocalSnapSlot.Full;

        return LocalSnapSlot.Free;
    }

    public static LocalSnapSlot MatchSnapRect(PixelRect container, PixelRect rect, int tolerance = 12)
    {
        if (container.Width < 2 || container.Height < 2 || rect.Width <= 0 || rect.Height <= 0)
            return LocalSnapSlot.Free;

        tolerance = Math.Max(0, tolerance);
        var candidates = new[]
        {
            LocalSnapSlot.Full,
            LocalSnapSlot.Left,
            LocalSnapSlot.Right,
            LocalSnapSlot.TopLeft,
            LocalSnapSlot.TopRight,
            LocalSnapSlot.BottomLeft,
            LocalSnapSlot.BottomRight
        };

        foreach (var slot in candidates)
        {
            var candidate = LocalSnapRect(container, slot);
            if (Math.Abs(candidate.X - rect.X) <= tolerance &&
                Math.Abs(candidate.Y - rect.Y) <= tolerance &&
                Math.Abs(candidate.Width - rect.Width) <= tolerance &&
                Math.Abs(candidate.Height - rect.Height) <= tolerance)
                return slot;
        }

        return LocalSnapSlot.Free;
    }

    public static bool IsInside(PixelRect inner, PixelRect outer) =>
        inner.Width > 0 &&
        inner.Height > 0 &&
        inner.X >= outer.X &&
        inner.Y >= outer.Y &&
        inner.Right <= outer.Right &&
        inner.Bottom <= outer.Bottom;

    public static LayoutNode Replace(LayoutNode root, string id, Func<ZoneNode, LayoutNode> change)
    {
        if (root is ZoneNode z)
            return z.Id == id ? change(z) : z;

        var s = (SplitNode)root;
        s.First = Replace(s.First, id, change);
        s.Second = Replace(s.Second, id, change);
        return s;
    }

    // Removing a leaf collapses its parent to the surviving sibling subtree.
    public static LayoutNode Remove(LayoutNode root, string id)
    {
        if (root is not SplitNode s)
            return root;

        if (s.First is ZoneNode a && a.Id == id)
            return s.Second;
        if (s.Second is ZoneNode b && b.Id == id)
            return s.First;

        s.First = Remove(s.First, id);
        s.Second = Remove(s.Second, id);
        return s;
    }
}

public static class RuleMatcher
{
    public static WindowRule? Match(
        IEnumerable<WindowRule> rules,
        WindowIdentity identity,
        string title,
        string layoutId,
        bool manualOverride) =>
        manualOverride
            ? null
            : rules.FirstOrDefault(r =>
                r.LayoutId == layoutId &&
                !string.IsNullOrWhiteSpace(r.ProcessName) &&
                string.Equals(
                    Path.GetFileNameWithoutExtension(r.ProcessName),
                    Path.GetFileNameWithoutExtension(identity.ProcessName),
                    StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(r.WindowClass) ||
                 string.Equals(r.WindowClass, identity.WindowClass, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(r.TitleContains) ||
                 title.Contains(r.TitleContains, StringComparison.OrdinalIgnoreCase)));
}
