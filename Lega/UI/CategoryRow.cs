using System.ComponentModel;
using System.Drawing.Drawing2D;
using Lega.Core;

namespace Lega.UI;

public enum RowState { Scanning, Ready, Cleaning }

/// <summary>Satu baris kategori: checkbox, judul, pil risiko, deskripsi, ukuran, dan porsinya dibanding yang terbesar.</summary>
public sealed class CategoryRow : Control
{
    private const TextFormatFlags OneLine =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

    // Ukur tanpa EndEllipsis: dengan Size.Empty, flag itu membuat lebar terukur lebih kecil dari yang digambar.
    private const TextFormatFlags Measure =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    private bool _hover, _checked, _frozen;
    private RowState _state = RowState.Scanning;
    private long _bytes;
    private double _share;
    private string? _note;
    private Color? _noteColor;

    public CleanCategory Category { get; }
    public bool Locked { get; }
    public event EventHandler? CheckedChanged;

    public CategoryRow(CleanCategory category, bool locked)
    {
        Category = category;
        Locked = locked;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Margin = Padding.Empty;
        AccessibleName = category.Title;
        AccessibleDescription = category.Description;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RowState State { get => _state; set { _state = value; Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long Bytes { get => _bytes; set { _bytes = value; Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Share { get => _share; set { _share = value; Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Frozen { get => _frozen; set { _frozen = value; Invalidate(); } }

    public void SetNote(string? text, Color? color = null)
    {
        _note = text;
        _noteColor = color;
        Invalidate();
    }

    public bool CanToggle =>
        !Locked && !_frozen && _state == RowState.Ready && (_bytes > 0 || !Category.SizeKnown);

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Toggle()
    {
        if (CanToggle) Checked = !Checked;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool can = CanToggle;
        bool empty = _state == RowState.Ready && _bytes == 0 && Category.SizeKnown;
        g.Clear(_hover && can ? Theme.Hover : Theme.Surface);

        float pad = this.Px(32), w = Width, h = Height;

        // Tepi kunyit: baris ini akan dikosongkan.
        if (_checked)
        {
            using var mark = new SolidBrush(can || _frozen ? Theme.Turmeric : Theme.Track);
            g.FillRectangle(mark, 0, 0, this.Px(4), h);
        }

        // Checkbox
        float cb = this.Px(20), radius = this.Px(4);
        var box = new RectangleF(pad, (h - cb) / 2f, cb, cb);
        if (_checked)
        {
            using (var b = new SolidBrush(can ? Theme.Accent : Theme.Edge)) Gfx.FillRound(g, b, box, radius);
            using var pen = new Pen(Color.White, this.Px(2f))
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round,
            };
            g.DrawLines(pen, new[]
            {
                new PointF(box.X + box.Width * .27f, box.Y + box.Height * .52f),
                new PointF(box.X + box.Width * .43f, box.Y + box.Height * .68f),
                new PointF(box.X + box.Width * .74f, box.Y + box.Height * .34f),
            });
        }
        else
        {
            using var pen = new Pen(can ? Theme.Edge : Theme.Track, this.Px(1.5f));
            var inner = RectangleF.Inflate(box, -pen.Width / 2f, -pen.Width / 2f);
            using (var b = new SolidBrush(can ? Theme.Surface : Theme.Paper)) Gfx.FillRound(g, b, inner, radius);
            Gfx.DrawRound(g, pen, inner, radius);
        }

        // Kolom kanan
        float rightW = this.Px(170);
        float rightX = w - pad - rightW;
        float textX = pad + cb + this.Px(18);
        float textRight = rightX - this.Px(16);
        int availW = Math.Max(0, (int)(textRight - textX));

        // Judul + pil risiko
        var titleFont = Theme.Semi(10.5f);
        int ty = (int)this.Px(13), th = (int)this.Px(22);
        var titleSize = TextRenderer.MeasureText(g, Category.Title, titleFont, new Size(availW, th), OneLine);
        TextRenderer.DrawText(g, Category.Title, titleFont, new Rectangle((int)textX, ty, availW, th),
            empty || Locked ? Theme.Muted : Theme.Ink, OneLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        var (pill, pillText) = Category.Risk switch
        {
            Risk.Safe => (Theme.SafePill, "Aman"),
            Risk.Review => (Theme.ReviewPill, "Tinjau dulu"),
            _ => (Theme.CarefulPill, "Hati-hati"),
        };
        var pillFont = Theme.Semi(8f);
        var pillSize = TextRenderer.MeasureText(g, pillText, pillFont, Size.Empty, Measure);
        float pillW = pillSize.Width + this.Px(14), pillH = this.Px(18);
        float pillX = textX + titleSize.Width + this.Px(10);
        if (pillX + pillW <= textRight)
        {
            var pr = new RectangleF(pillX, ty + (th - pillH) / 2f, pillW, pillH);
            using (var b = new SolidBrush(pill.Back)) Gfx.FillRound(g, b, pr, pillH / 2f);
            TextRenderer.DrawText(g, pillText, pillFont, Rectangle.Round(pr), pill.Fore,
                OneLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // Deskripsi (maksimal dua baris)
        TextRenderer.DrawText(g, Category.Description, Theme.Text(9f),
            Gfx.Rect(textX, this.Px(37), availW, this.Px(32)), Theme.Muted,
            TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.Top);

        // Ukuran
        bool hasSize = _state == RowState.Ready && _bytes > 0;
        string sizeText = _state switch
        {
            RowState.Scanning => "Memindai…",
            RowState.Cleaning => "Membersihkan…",
            _ when _bytes > 0 => Format.Bytes(_bytes),
            _ when !Category.SizeKnown => "—",
            _ => "Kosong",
        };
        Color sizeColor = _state == RowState.Cleaning ? Theme.Accent : hasSize && !Locked ? Theme.Ink : Theme.Muted;
        var sizeFont = hasSize ? Theme.Gauge(15f) : Theme.Text(9.5f);
        TextRenderer.DrawText(g, sizeText, sizeFont, Gfx.Rect(rightX, this.Px(10), rightW, this.Px(28)),
            sizeColor, OneLine | TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        // Catatan, dengan ikon untuk kunci dan hasil
        string? note = _note ?? (Locked ? "Butuh administrator" : !Category.SizeKnown ? "Ukuran dihitung Windows" : null);
        if (note != null)
        {
            Color nc = _noteColor ?? Theme.Muted;
            float noteY = this.Px(39), noteH = this.Px(18), textW = rightW;

            // Ikon di ujung kanan, teks rata kanan sebelum ikon.
            string? glyph = _note == null && Locked ? Theme.GlyphLock
                : _noteColor == Theme.Accent ? Theme.GlyphCheck : null;
            if (glyph != null)
            {
                float gw = this.Px(14);
                TextRenderer.DrawText(g, glyph, Theme.Icon(8.5f), Gfx.Rect(rightX + rightW - gw, noteY, gw, noteH),
                    nc, OneLine | TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                textW -= gw + this.Px(6);
            }

            TextRenderer.DrawText(g, note, Theme.Text(9f), Gfx.Rect(rightX, noteY, textW, noteH), nc,
                OneLine | TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        // Porsi dibanding kategori terbesar; tumbuh dari kanan, menempel ke angka ukuran.
        if (hasSize && Category.SizeKnown)
        {
            float barW = this.Px(120), barH = this.Px(4);
            float bx = w - pad - barW, by = this.Px(63);
            using (var tb = new SolidBrush(Theme.Line)) g.FillRectangle(tb, bx, by, barW, barH);
            float fw = Math.Max(barH, (float)(barW * Math.Clamp(_share, 0, 1)));
            var fill = new RectangleF(bx + barW - fw, by, fw, barH);
            if (_checked && can)
            {
                using (var fb = new SolidBrush(Theme.Turmeric)) g.FillRectangle(fb, fill);
                Gfx.Hatch(g, fill, Theme.TurmericDeep, this.Px(1f), this.Px(4));
            }
            else
            {
                using var fb = new SolidBrush(Theme.Edge);
                g.FillRectangle(fb, fill);
            }
        }

        // Pemisah
        using (var line = new Pen(Theme.Line, 1f))
            g.DrawLine(line, pad, h - 1, w - pad, h - 1);

        // Fokus keyboard
        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Theme.Accent, this.Px(2f));
            Gfx.DrawRound(g, ring, new RectangleF(this.Px(10), this.Px(3), w - this.Px(20), h - this.Px(7)), this.Px(6));
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Cursor = CanToggle ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) Toggle();
        base.OnMouseUp(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var siblings = Parent?.Controls;
        switch (e.KeyCode)
        {
            case Keys.Space:
                e.Handled = true;
                Toggle();
                break;
            case Keys.Up or Keys.Down when siblings != null:
            {
                e.Handled = true;
                int i = siblings.GetChildIndex(this) + (e.KeyCode == Keys.Down ? 1 : -1);
                if (i >= 0 && i < siblings.Count) siblings[i].Focus();
                break;
            }
            case Keys.Home or Keys.End when siblings is { Count: > 0 }:
                e.Handled = true;
                siblings[e.KeyCode == Keys.Home ? 0 : siblings.Count - 1].Focus();
                break;
        }
        base.OnKeyDown(e);
    }

    protected override void OnEnter(EventArgs e)
    {
        (Parent as ScrollableControl)?.ScrollControlIntoView(this);
        base.OnEnter(e);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new RowAccessibility(this);

    private sealed class RowAccessibility(CategoryRow row) : ControlAccessibleObject(row)
    {
        public override AccessibleRole Role => AccessibleRole.CheckButton;

        public override AccessibleStates State
        {
            get
            {
                var s = base.State;
                if (row.Checked) s |= AccessibleStates.Checked;
                if (!row.CanToggle) s |= AccessibleStates.Unavailable;
                return s;
            }
        }

        public override string DefaultAction => "Centang atau hapus centang";
        public override void DoDefaultAction() => row.Toggle();
    }
}
