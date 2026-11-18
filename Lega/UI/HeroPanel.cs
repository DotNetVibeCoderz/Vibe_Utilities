using System.Drawing.Drawing2D;
using Lega.Core;

namespace Lega.UI;

/// <summary>Apa yang ditampilkan panel instrumen.</summary>
/// <param name="Eyebrow">Label kapital kecil di atas angka.</param>
/// <param name="Figure">Angka besar dalam byte; null menampilkan tanda pisah.</param>
/// <param name="Sentence">Satu kalimat penjelas di bawah angka.</param>
/// <param name="Reclaim">True bila angka besar berarti ruang yang dikosongkan (diwarnai kunyit).</param>
/// <param name="CountUp">Angka dihitung naik dari nol saat label berganti (momen "selesai").</param>
public sealed record HeroReading(string Eyebrow, long? Figure, string Sentence, bool Reclaim = false, bool CountUp = false);

/// <summary>
/// Panel instrumen di atas: label, angka besar Bahnschrift, satu kalimat, lalu meter drive C dengan skala.
/// Meter punya tiga bagian: tetap terpakai (sage), bisa dikosongkan (kunyit berarsir), bebas (gelap).
/// Angka dan meter bergerak bersama; tanpa animasi bila efek UI Windows dimatikan.
/// </summary>
public sealed class HeroPanel : Control
{
    private const TextFormatFlags OneLine = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };

    private HeroReading _reading = new("", null, "");
    private long _total, _used, _reclaim;
    private double _figNow, _figTo;
    private float _keptNow, _reclNow, _keptTo, _reclTo;

    public HeroPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Pine;
        AccessibleRole = AccessibleRole.StaticText;
        _timer.Tick += Step;
    }

    public int PreferredHeight => (int)this.Px(214);

    private static bool Animate => SystemInformation.UIEffectsEnabled;

    public void Show(HeroReading reading, long total, long used, long reclaim)
    {
        bool relabel = reading.Eyebrow != _reading.Eyebrow;
        _reading = reading;

        _total = Math.Max(0, total);
        _used = Math.Clamp(used, 0, _total);
        _reclaim = Math.Clamp(reclaim, 0, _used);
        _keptTo = _total > 0 ? (float)(_used - _reclaim) / _total : 0;
        _reclTo = _total > 0 ? (float)_reclaim / _total : 0;

        _figTo = reading.Figure ?? 0;
        if (relabel) _figNow = reading.CountUp ? 0 : _figTo;

        string figure = reading.Figure is { } f ? Format.Bytes(f) : "tidak diketahui";
        AccessibleName = $"{reading.Eyebrow}: {figure}. {reading.Sentence} " +
                         $"Drive C: {Format.Bytes(_total - _used)} bebas dari {Format.Bytes(_total)}.";

        if (Animate) _timer.Start();
        else { Settle(); Invalidate(); }
    }

    private void Settle()
    {
        _figNow = _figTo;
        _keptNow = _keptTo;
        _reclNow = _reclTo;
    }

    private void Step(object? sender, EventArgs e)
    {
        const float k = 0.14f;
        _keptNow += (_keptTo - _keptNow) * k;
        _reclNow += (_reclTo - _reclNow) * k;
        _figNow += (_figTo - _figNow) * k;

        bool done = Math.Abs(_keptTo - _keptNow) < 0.0004f &&
                    Math.Abs(_reclTo - _reclNow) < 0.0004f &&
                    Math.Abs(_figTo - _figNow) < Math.Max(1, _figTo * 0.0005);
        if (done)
        {
            Settle();
            _timer.Stop();
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        float pad = this.Px(32), w = Width - pad * 2;
        if (w <= 0) return;

        // Label
        Color figColor = _reading.Reclaim ? Theme.Turmeric : Theme.OnPine;
        if (_reading.Reclaim)
        {
            using var b = new SolidBrush(Theme.Turmeric);
            g.FillRectangle(b, pad, this.Px(13), this.Px(8), this.Px(8));
        }
        float eyebrowX = pad + (_reading.Reclaim ? this.Px(16) : 0);
        Gfx.DrawTracked(g, _reading.Eyebrow.ToUpperInvariant(), Theme.Label(8.5f), eyebrowX, this.Px(8),
            _reading.Reclaim ? Theme.Turmeric : Theme.OnPineMuted, this.Px(1.6f));

        // Angka besar + satuan
        string text = _reading.Figure is null ? "—" : Format.Bytes((long)Math.Round(_figNow));
        int split = text.LastIndexOf(' ');
        string number = split > 0 ? text[..split] : text;
        string unit = split > 0 ? text[(split + 1)..] : "";

        var numFont = Theme.Gauge(40f);
        var unitFont = Theme.GaugeLight(20f);
        var numSize = TextRenderer.MeasureText(g, number, numFont, Size.Empty, OneLine);
        float numY = this.Px(24);
        TextRenderer.DrawText(g, number, numFont, new Point((int)pad, (int)numY), figColor, OneLine);
        if (unit.Length > 0)
        {
            var unitSize = TextRenderer.MeasureText(g, unit, unitFont, Size.Empty, OneLine);
            // Sejajarkan dasar huruf satuan dengan dasar angka.
            float baseline = numY + numSize.Height * 0.80f;
            float unitY = baseline - unitSize.Height * 0.80f;
            TextRenderer.DrawText(g, unit, unitFont, new Point((int)(pad + numSize.Width + this.Px(6)), (int)unitY),
                _reading.Reclaim ? Theme.Turmeric : Theme.OnPineMuted, OneLine);
        }

        // Kalimat
        TextRenderer.DrawText(g, _reading.Sentence, Theme.Text(10.5f), Gfx.Rect(pad, this.Px(88), w, this.Px(22)),
            Theme.OnPine, OneLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

        if (_total <= 0) return;

        // Skala
        var scaleFont = Theme.Label(7.5f);
        float scaleY = this.Px(122);
        DrawScaleLabel(g, "0", scaleFont, pad, scaleY, TextFormatFlags.Left);
        DrawScaleLabel(g, "50%", scaleFont, pad + w / 2f, scaleY, TextFormatFlags.HorizontalCenter);
        DrawScaleLabel(g, Format.Bytes(_total), scaleFont, pad + w, scaleY, TextFormatFlags.Right);

        float barY = this.Px(146), barH = this.Px(24);
        using (var minor = new Pen(Theme.PineLine, 1f))
        using (var major = new Pen(Theme.OnPineMuted, 1f))
        {
            for (int i = 0; i <= 20; i++)
            {
                float x = pad + w * i / 20f;
                if (i == 20) x -= 1;
                bool isMajor = i % 10 == 0;
                float len = isMajor ? this.Px(8) : i % 2 == 0 ? this.Px(5) : this.Px(3);
                g.DrawLine(isMajor ? major : minor, x, barY - this.Px(3) - len, x, barY - this.Px(3));
            }
        }

        // Meter
        var track = new RectangleF(pad, barY, w, barH);
        float radius = this.Px(3);
        using (var clip = Gfx.Round(track, radius))
        {
            using (var tb = new SolidBrush(Theme.PineDeep)) g.FillPath(tb, clip);

            var state = g.Save();
            g.SetClip(clip, CombineMode.Intersect);

            float keptW = w * _keptNow, reclW = w * _reclNow;
            if (keptW > 0.5f)
            {
                using var sb = new SolidBrush(Theme.Sage);
                g.FillRectangle(sb, pad, barY, keptW, barH);
            }
            if (reclW > 0.5f)
            {
                var r = new RectangleF(pad + keptW, barY, reclW, barH);
                using (var ab = new SolidBrush(Theme.Turmeric)) g.FillRectangle(ab, r);
                Gfx.Hatch(g, r, Theme.TurmericDeep, this.Px(2f), this.Px(7));
                // Garis tegas di batas: di sinilah pemakaian berhenti setelah dibersihkan.
                using var edge = new Pen(Theme.Pine, this.Px(2f));
                g.DrawLine(edge, r.Left, barY, r.Left, barY + barH);
            }
            g.Restore(state);
        }

        // Keterangan
        float ly = this.Px(184);
        float x0 = pad;
        long free = _total - _used;
        string freeValue = _reclaim > 0 ? $"{Format.Bytes(free)} → {Format.Bytes(free + _reclaim)}" : Format.Bytes(free);
        x0 = LegendItem(g, x0, ly, Swatch.Solid, "Tetap terpakai", Format.Bytes(_used - _reclaim));
        x0 = LegendItem(g, x0, ly, Swatch.Hatched, "Bisa dikosongkan", Format.Bytes(_reclaim));
        LegendItem(g, x0, ly, Swatch.Empty, _reclaim > 0 ? "Bebas sesudahnya" : "Bebas", freeValue);
    }

    private void DrawScaleLabel(Graphics g, string text, Font font, float x, float y, TextFormatFlags align)
    {
        int tw = TextRenderer.MeasureText(g, text, font, Size.Empty, OneLine).Width + 2;
        float left = align switch
        {
            TextFormatFlags.Right => x - tw,
            TextFormatFlags.HorizontalCenter => x - tw / 2f,
            _ => x,
        };
        TextRenderer.DrawText(g, text, font, Gfx.Rect(left, y, tw, this.Px(14)), Theme.OnPineMuted, OneLine);
    }

    private enum Swatch { Solid, Hatched, Empty }

    private float LegendItem(Graphics g, float x, float y, Swatch kind, string label, string value)
    {
        float d = this.Px(10);
        var sw = new RectangleF(x, y + this.Px(4), d, d);
        switch (kind)
        {
            case Swatch.Solid:
                using (var b = new SolidBrush(Theme.Sage)) g.FillRectangle(b, sw);
                break;
            case Swatch.Hatched:
                using (var b = new SolidBrush(Theme.Turmeric)) g.FillRectangle(b, sw);
                Gfx.Hatch(g, sw, Theme.TurmericDeep, this.Px(1.5f), this.Px(4));
                break;
            default:
                using (var b = new SolidBrush(Theme.PineDeep)) g.FillRectangle(b, sw);
                using (var p = new Pen(Theme.OnPineMuted, 1f)) g.DrawRectangle(p, sw.X, sw.Y, sw.Width - 1, sw.Height - 1);
                break;
        }
        x += d + this.Px(8);

        var lf = Theme.Text(9.5f);
        var vf = Theme.Gauge(10.5f);
        int lw = TextRenderer.MeasureText(g, label, lf, Size.Empty, OneLine).Width;
        TextRenderer.DrawText(g, label, lf, Gfx.Rect(x, y, lw + 2, this.Px(18)), Theme.OnPineMuted, OneLine | TextFormatFlags.VerticalCenter);
        x += lw + this.Px(6);
        int vw = TextRenderer.MeasureText(g, value, vf, Size.Empty, OneLine).Width;
        TextRenderer.DrawText(g, value, vf, Gfx.Rect(x, y, vw + 2, this.Px(18)), Theme.OnPine, OneLine | TextFormatFlags.VerticalCenter);
        return x + vw + this.Px(28);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
