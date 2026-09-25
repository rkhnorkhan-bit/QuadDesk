using System.Drawing.Drawing2D;

namespace QuadDesk.UI;

internal sealed class CursorFinderOverlayForm : Form
{
    const int WsExTransparent = 0x00000020;
    const int WsExToolWindow = 0x00000080;
    const int WsExNoActivate = 0x08000000;

    readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    DateTimeOffset startedAt;
    int durationMs = 650;
    int visualSize = 96;

    public CursorFinderOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;

        timer.Tick += (_, _) =>
        {
            if (!Visible)
                return;

            double elapsed = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            if (elapsed >= durationMs)
            {
                Hide();
                timer.Stop();
                return;
            }

            Reposition();
            Invalidate();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            return cp;
        }
    }

    public void Pulse(Point cursor, int size, int fadeMs)
    {
        visualSize = Math.Clamp(size, 48, 256);
        durationMs = Math.Clamp(fadeMs, 150, 3000);
        startedAt = DateTimeOffset.UtcNow;
        Reposition(cursor);

        if (!Visible)
            Show();

        Invalidate();
        timer.Stop();
        timer.Start();
    }

    public void HideNow()
    {
        timer.Stop();
        Hide();
    }

    void Reposition() => Reposition(Cursor.Position);

    void Reposition(Point cursor)
    {
        int margin = Math.Max(24, visualSize / 3);
        Bounds = new Rectangle(cursor.X - margin, cursor.Y - margin, visualSize + margin * 2, visualSize + margin * 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        double elapsed = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        float strength = (float)Math.Clamp(1d - elapsed / Math.Max(1, durationMs), 0d, 1d);
        if (strength <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int margin = Math.Max(24, visualSize / 3);
        var cursorRect = new Rectangle(margin, margin, visualSize, visualSize);
        var haloRect = Rectangle.Inflate(cursorRect, visualSize / 5, visualSize / 5);

        int glowAlpha = Math.Clamp((int)(70 * strength), 0, 70);
        int lineAlpha = Math.Clamp((int)(210 * strength), 0, 210);
        using var glow = new SolidBrush(Color.FromArgb(glowAlpha, SystemColors.Highlight));
        using var outer = new Pen(Color.FromArgb(lineAlpha, Color.White), Math.Max(2, visualSize / 26f));
        using var inner = new Pen(Color.FromArgb(Math.Clamp((int)(160 * strength), 0, 160), SystemColors.Highlight), Math.Max(2, visualSize / 34f));

        e.Graphics.FillEllipse(glow, haloRect);
        e.Graphics.DrawEllipse(outer, haloRect);
        e.Graphics.DrawEllipse(inner, Rectangle.Inflate(haloRect, -8, -8));

        Cursors.Default.DrawStretched(e.Graphics, cursorRect);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            timer.Dispose();
        base.Dispose(disposing);
    }
}
