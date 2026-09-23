using QuadDesk.Core;
namespace QuadDesk.UI;
internal sealed class ZoneOverlayForm : Form
{
    IReadOnlyList<Zone> zones = [];
    string? candidate;
    PixelRect monitor;
    public ZoneOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        BackColor = Color.FromArgb(15, 27, 42); Opacity = .32; DoubleBuffered = true;
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020 | 0x00080000; return p; } }
    public void Display(PixelRect bounds, IReadOnlyList<Zone> value, string? selected)
    {
        monitor = bounds; zones = value; candidate = selected;
        Bounds = new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        if (!Visible) Show(); Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); using var font = new Font("Segoe UI", 22, FontStyle.Bold); using var pen = new Pen(Color.White, 3); using var fill = new SolidBrush(Color.DodgerBlue);
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i]; var r = new Rectangle(z.Bounds.X - monitor.X, z.Bounds.Y - monitor.Y, z.Bounds.Width, z.Bounds.Height);
            r.Inflate(-3, -3); if (z.Id == candidate) e.Graphics.FillRectangle(fill, r);
            e.Graphics.DrawRectangle(pen, r); e.Graphics.DrawString($"{i + 1}  {z.Name}", font, Brushes.White, r.X + 16, r.Y + 16);
        }
    }
}
