using QuadDesk.Core;
using System.ComponentModel;
using LayoutMath = QuadDesk.Core.LayoutEngine;
namespace QuadDesk.UI;
internal sealed class LayoutPreviewControl : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LayoutDefinition? CurrentLayout { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PixelRect Monitor { get; set; } = new(0, 0, 3840, 2160);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Editable { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MinimumWidth { get; set; } = 200;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MinimumHeight { get; set; } = 150;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedZoneId { get; set; }
    public event Action? SelectionChanged;
    public event Action? TreeChanged;
    readonly List<(SplitNode Node, PixelRect Bounds, RectangleF Handle)> splitters = [];
    List<Zone> zones = [];
    (SplitNode Node, PixelRect Bounds)? dragging;
    RectangleF viewport;
    public LayoutPreviewControl() { DoubleBuffered = true; BackColor = Color.FromArgb(20, 29, 43); ForeColor = Color.White; MinimumSize = new(260, 180); }
    RectangleF Map(PixelRect r)
    {
        float scale = viewport.Width / Monitor.Width;
        return new(viewport.X + (r.X - Monitor.X) * scale, viewport.Y + (r.Y - Monitor.Y) * scale, r.Width * scale, r.Height * scale);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); splitters.Clear(); zones.Clear();
        if (CurrentLayout is null || Monitor.Width <= 0 || Monitor.Height <= 0) return;
        float scale = Math.Min((ClientSize.Width - 32f) / Monitor.Width, (ClientSize.Height - 32f) / Monitor.Height);
        if (scale <= 0) return;
        viewport = new((ClientSize.Width - Monitor.Width * scale) / 2, (ClientSize.Height - Monitor.Height * scale) / 2, Monitor.Width * scale, Monitor.Height * scale);
        try { zones = LayoutMath.Calculate(CurrentLayout, Monitor); } catch (ArgumentException) { return; }
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i]; var r = Map(z.Bounds); r.Inflate(-2, -2);
            using var fill = new SolidBrush(z.Id == SelectedZoneId ? Color.FromArgb(26, 99, 156) : Color.FromArgb(39, 56, 78));
            e.Graphics.FillRectangle(fill, r); using var pen = new Pen(Color.FromArgb(110, 162, 205)); e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            if (r.Width > 55 && r.Height > 35)
            {
                string text = $"{i + 1} · {z.Name}\n{z.Bounds.Width} × {z.Bounds.Height} px";
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                e.Graphics.DrawString(text, Font, Brushes.White, r, format);
            }
        }
        void Walk(LayoutNode node, PixelRect r)
        {
            if (node is not SplitNode s) return;
            var (a, b) = LayoutMath.Divide(r, s); var mapped = Map(r); var cut = Map(a);
            var handle = s.Orientation == SplitOrientation.Vertical ? new RectangleF(cut.Right - 5, mapped.Y, 10, mapped.Height) : new RectangleF(mapped.X, cut.Bottom - 5, mapped.Width, 10);
            splitters.Add((s, r, handle)); Walk(s.First, a); Walk(s.Second, b);
        }
        Walk(CurrentLayout.Root, Monitor);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (!Editable || e.Button != MouseButtons.Left) return;
        foreach (var s in splitters.AsEnumerable().Reverse())
            if (s.Handle.Contains(e.Location)) { dragging = (s.Node, s.Bounds); Capture = true; return; }
        SelectedZoneId = zones.FirstOrDefault(z => Map(z.Bounds).Contains(e.Location))?.Id; Invalidate(); SelectionChanged?.Invoke();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!Editable) return;
        if (dragging is not { } drag || CurrentLayout is null)
        { var split = splitters.LastOrDefault(s => s.Handle.Contains(e.Location)); Cursor = split.Node is null ? Cursors.Default : split.Node.Orientation == SplitOrientation.Vertical ? Cursors.VSplit : Cursors.HSplit; return; }
        double scale = viewport.Width / Monitor.Width;
        double value = drag.Node.Orientation == SplitOrientation.Vertical ? ((e.X - viewport.X) / scale + Monitor.X - drag.Bounds.X) / drag.Bounds.Width : ((e.Y - viewport.Y) / scale + Monitor.Y - drag.Bounds.Y) / drag.Bounds.Height;
        double old = drag.Node.Ratio; drag.Node.Ratio = Math.Clamp(value, .001, .999);
        try { _ = LayoutMath.Calculate(CurrentLayout, Monitor, MinimumWidth, MinimumHeight); }
        catch (ArgumentException) { drag.Node.Ratio = old; }
        Invalidate(); SelectionChanged?.Invoke();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e); if (dragging is null) return; dragging = null; Capture = false; TreeChanged?.Invoke();
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}
