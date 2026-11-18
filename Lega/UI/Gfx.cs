using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Lega.UI;

public static class Gfx
{
    private const TextFormatFlags Glyph = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

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

    /// <summary>Garis miring "akan dikosongkan" di dalam persegi, seperti arsiran di papan meter.</summary>
    public static void Hatch(Graphics g, RectangleF r, Color color, float width, float spacing)
    {
        if (r.Width <= 0.5f || r.Height <= 0.5f) return;
        var state = g.Save();
        g.SetClip(r, CombineMode.Intersect);
        using var pen = new Pen(color, width);
        for (float x = r.Left - r.Height; x < r.Right; x += spacing)
            g.DrawLine(pen, x, r.Bottom, x + r.Height, r.Top);
        g.Restore(state);
    }

    /// <summary>Teks kapital dengan jarak huruf (GDI tidak punya letter-spacing). Mengembalikan lebarnya.</summary>
    public static float DrawTracked(Graphics g, string text, Font font, float x, float y, Color color, float tracking)
    {
        float start = x;
        foreach (char ch in text)
        {
            string s = ch.ToString();
            int w = ch == ' '
                ? TextRenderer.MeasureText(g, "n", font, Size.Empty, Glyph).Width / 2 + 1
                : TextRenderer.MeasureText(g, s, font, Size.Empty, Glyph).Width;
            if (ch != ' ')
                TextRenderer.DrawText(g, s, font, new Point((int)Math.Round(x), (int)Math.Round(y)), color, Glyph);
            x += w + tracking;
        }
        return x - start - tracking;
    }

    public static float MeasureTracked(Graphics g, string text, Font font, float tracking)
    {
        float w = 0;
        foreach (char ch in text)
            w += (ch == ' '
                ? TextRenderer.MeasureText(g, "n", font, Size.Empty, Glyph).Width / 2 + 1
                : TextRenderer.MeasureText(g, ch.ToString(), font, Size.Empty, Glyph).Width) + tracking;
        return Math.Max(0, w - tracking);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Warnai bilah judul Windows 11 supaya menyatu dengan panel atas. Diam saja di Windows 10.</summary>
    public static void TintTitleBar(IntPtr hwnd, Color caption, Color text)
    {
        try
        {
            int c = ColorTranslator.ToWin32(caption), t = ColorTranslator.ToWin32(text), dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));   // DWMWA_USE_IMMERSIVE_DARK_MODE
            DwmSetWindowAttribute(hwnd, 35, ref c, sizeof(int));      // DWMWA_CAPTION_COLOR
            DwmSetWindowAttribute(hwnd, 36, ref t, sizeof(int));      // DWMWA_TEXT_COLOR
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
}

/// <summary>Panel tanpa kedip dengan warna latar tetap dan garis tepi opsional.</summary>
public sealed class Band : Panel
{
    public Color? TopRule { get; set; }

    public Band(Color back)
    {
        BackColor = back;
        DoubleBuffered = true;
        Margin = Padding.Empty;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (TopRule is { } c)
        {
            using var pen = new Pen(c, 1f);
            e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }
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
