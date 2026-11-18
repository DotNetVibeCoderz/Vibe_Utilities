using System.Drawing.Drawing2D;
using Lega.Core;

namespace Lega.UI;

/// <summary>
/// Bar penyimpanan drive. Tiga bagian: tetap terpakai (slate), bisa dikosongkan (amber), bebas (kosong).
/// Saat pilihan berubah atau pembersihan selesai, amber mengecil dan area bebas melebar.
/// </summary>
public sealed class StorageBar : Control
{
    private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private long _total, _used, _reclaim;
    private float _keptNow, _reclNow, _keptTo, _reclTo;

    public StorageBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Mist;
        AccessibleRole = AccessibleRole.Graphic;
        _timer.Tick += Step;
    }

    public int PreferredHeight => (int)(this.Px(20) + this.Px(14) + this.Px(22));

    public void SetValues(long total, long used, long reclaim)
    {
        _total = Math.Max(0, total);
        _used = Math.Clamp(used, 0, _total);
        _reclaim = Math.Clamp(reclaim, 0, _used);

        if (_total > 0)
        {
            _keptTo = (float)(_used - _reclaim) / _total;
            _reclTo = (float)_reclaim / _total;
        }
        else
        {
            _keptTo = _reclTo = 0;
        }

        AccessibleName = $"Drive C: {Format.Bytes(_total - _used)} bebas dari {Format.Bytes(_total)}. " +
                         $"{Format.Bytes(_reclaim)} bisa dikosongkan.";

        if (!SystemInformation.UIEffectsEnabled)
        {
            _keptNow = _keptTo;
            _reclNow = _reclTo;
            Invalidate();
        }
        else
        {
            _timer.Start();
        }
    }

    private void Step(object? sender, EventArgs e)
    {
        _keptNow += (_keptTo - _keptNow) * 0.16f;
        _reclNow += (_reclTo - _reclNow) * 0.16f;

        if (Math.Abs(_keptTo - _keptNow) < 0.0004f && Math.Abs(_reclTo - _reclNow) < 0.0004f)
        {
            _keptNow = _keptTo;
            _reclNow = _reclTo;
            _timer.Stop();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        float barH = this.Px(20);
        var track = new RectangleF(0, 0, Width, barH);

        using (var clip = Gfx.Round(track, barH / 2f))
        {
            using (var tb = new SolidBrush(Theme.Track)) g.FillPath(tb, clip);

            var state = g.Save();
            g.SetClip(clip, CombineMode.Intersect);

            float keptW = Width * _keptNow;
            float reclW = Width * _reclNow;
            float gap = keptW > 0.5f ? this.Px(3) : 0f;

            if (keptW > 0.5f)
            {
                using var sb = new SolidBrush(Theme.Slate);
                g.FillRectangle(sb, 0, 0, keptW, barH);
            }

            if (reclW - gap > 0.5f)
            {
                using var ab = new SolidBrush(Theme.Amber);
                g.FillRectangle(ab, keptW + gap, 0, reclW - gap, barH);
            }

            g.Restore(state);
        }

        if (_total <= 0) return;

        float y = barH + this.Px(14);
        float x = 0;
        var labelFont = Theme.Text(9.5f);
        var valueFont = Theme.Semi(9.5f);

        (Color color, string label, string value)[] items =
        [
            (Theme.Slate, "Tetap terpakai", Format.Bytes(_used - _reclaim)),
            (Theme.Amber, "Bisa dikosongkan", Format.Bytes(_reclaim)),
            (Theme.Track, "Bebas", Format.Bytes(_total - _used)),
        ];

        foreach (var (color, label, value) in items)
        {
            float d = this.Px(10);
            using (var b = new SolidBrush(color))
                g.FillEllipse(b, x, y + this.Px(3), d, d);
            x += this.Px(18);

            int lw = TextRenderer.MeasureText(g, label, labelFont, Size.Empty, Flags).Width;
            TextRenderer.DrawText(g, label, labelFont, Gfx.Rect(x, y, lw + 2, this.Px(18)), Theme.Muted, Flags);
            x += lw + this.Px(6);

            int vw = TextRenderer.MeasureText(g, value, valueFont, Size.Empty, Flags).Width;
            TextRenderer.DrawText(g, value, valueFont, Gfx.Rect(x, y, vw + 2, this.Px(18)), Theme.Ink, Flags);
            x += vw + this.Px(28);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
