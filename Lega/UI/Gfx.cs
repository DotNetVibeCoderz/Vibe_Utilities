using System.Drawing.Drawing2D;

namespace Lega.UI;

public static class Gfx
{
    /// <summary>Piksel di DPI monitor saat ini (96 DPI = 1:1).</summary>
    public static float Px(this Control c, float v) => v * c.DeviceDpi / 96f;

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }

        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Brush b, RectangleF r, float radius)
    {
        using var p = Round(r, radius);
        g.FillPath(b, p);
    }

    public static void DrawRound(Graphics g, Pen pen, RectangleF r, float radius)
    {
        using var p = Round(r, radius);
        g.DrawPath(pen, p);
    }

    public static Rectangle Rect(float x, float y, float w, float h) =>
        Rectangle.Round(new RectangleF(x, y, w, h));
}

/// <summary>Panel tanpa kedip dengan warna latar tetap.</summary>
public sealed class Band : Panel
{
    public Band(Color back)
    {
        BackColor = back;
        DoubleBuffered = true;
        Margin = Padding.Empty;
    }
}

public sealed class RowList : FlowLayoutPanel
{
    public RowList()
    {
        DoubleBuffered = true;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoScroll = true;
        BackColor = Theme.Surface;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
    }
}
