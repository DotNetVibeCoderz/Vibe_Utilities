using System.Drawing.Drawing2D;

namespace Lega.UI;

/// <summary>
/// Tanda Lega: meter tekanan kecil dengan jarum yang sudah turun. Lega = tekanan dilepas.
/// Satu sumber gambar untuk logo di header dan ikon jendela. Assets/lega.ico memakai geometri yang sama
/// (lihat installer/make-icon.ps1).
/// </summary>
public static class Logo
{
    public static void Draw(Graphics g, RectangleF r)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = Math.Min(r.Width, r.Height);
        float x0 = r.X + (r.Width - s) / 2f, y0 = r.Y + (r.Height - s) / 2f;
        PointF P(float u, float v) => new(x0 + u * s, y0 + v * s);

        using (var bg = new SolidBrush(Theme.Turmeric))
            Gfx.FillRound(g, bg, new RectangleF(x0, y0, s, s), s * 0.24f);

        // Busur skala
        var c = P(0.5f, 0.64f);
        float rad = s * 0.30f;
        using (var arc = new Pen(Theme.Pine, s * 0.095f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(arc, c.X - rad, c.Y - rad, rad * 2, rad * 2, 180, 180);

        // Jarum di posisi rendah
        double a = 208 * Math.PI / 180;
        float len = s * 0.26f;
        var tip = new PointF(c.X + (float)Math.Cos(a) * len, c.Y + (float)Math.Sin(a) * len);
        using (var needle = new Pen(Theme.Pine, s * 0.085f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(needle, c, tip);

        float hub = s * 0.15f;
        using (var b = new SolidBrush(Theme.Pine))
            g.FillEllipse(b, c.X - hub / 2, c.Y - hub / 2, hub, hub);
    }

    public static Icon MakeIcon(int size)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            Draw(g, new RectangleF(0, 0, size, size));
        }
        IntPtr h = bmp.GetHicon();
        return (Icon)Icon.FromHandle(h).Clone();
    }
}

/// <summary>Logo di kiri atas header.</summary>
public sealed class LogoMark : Control
{
    public LogoMark()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        AccessibleName = "Lega";
        AccessibleRole = AccessibleRole.Graphic;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        Logo.Draw(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f));
    }
}
