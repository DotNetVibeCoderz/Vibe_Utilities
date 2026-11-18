using System.Drawing.Drawing2D;
using Lega.Core;

namespace Lega.UI;

public enum RowState { Scanning, Ready, Cleaning }

/// <summary>Satu baris kategori: checkbox, judul, tingkat risiko, deskripsi, ukuran, dan porsinya dibanding yang terbesar.</summary>
public sealed class CategoryRow : Control
{
    private const TextFormatFlags OneLine =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

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

    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RowState State { get => _state; set { _state = value; Invalidate(); } }
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long Bytes { get => _bytes; set { _bytes = value; Invalidate(); } }
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double Share { get => _share; set { _share = value; Invalidate(); } }
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Frozen { get => _frozen; set { _frozen = value; Invalidate(); } }

    public void SetNote(string? text, Color? color = null)
    {
        _note = text;
        _noteColor = color;
        Invalidate();
    }

    public bool CanToggle =>
        !Locked && !_frozen && _state == RowState.Ready && (_bytes > 0 || !Category.SizeKnown);

    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
        g.Clear(_hover && can ? Theme.Hover : Theme.Surface);

        float pad = this.Px(32), w = Width, h = Height;

        // Checkbox
        float cb = this.Px(20), radius = this.Px(5);
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
            using (var b = new SolidBrush(can ? Theme.Surface : Theme.Mist)) Gfx.FillRound(g, b, inner, radius);
            Gfx.DrawRound(g, pen, inner, radius);
        }

        // Kolom kanan
        float rightW = this.Px(160);
        float rightX = w - pad - rightW;
        float textX = pad + cb + this.Px(16);
        float textRight = rightX - this.Px(16);
        int availW = Math.Max(0, (int)(textRight - textX));

        // Judul + tingkat risiko
        var titleFont = Theme.Semi(10.5f);
        int ty = (int)this.Px(14), th = (int)this.Px(22);
        var titleSize = TextRenderer.MeasureText(g, Category.Title, titleFont, new Size(availW, th), OneLine);
        TextRenderer.DrawText(g, Category.Title, titleFont, new Rectangle((int)textX, ty, availW, th),
            Theme.Ink, OneLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        var (chipColor, chipText) = Category.Risk switch
        {
            Risk.Safe => (Theme.Accent, "Aman"),
            Risk.Review => (Theme.Amber, "Tinjau dulu"),
            _ => (Theme.Danger, "Hati-hati"),
        };
        var chipFont = Theme.Text(9f);
        var chipSize = TextRenderer.MeasureText(g, chipText, chipFont, Size.Empty, OneLine);
        float chipX = textX + titleSize.Width + this.Px(12);
        if (chipX + this.Px(14) + chipSize.Width <= textRight)
        {
            float d = this.Px(8);
            using (var b = new SolidBrush(chipColor))
                g.FillEllipse(b, chipX, ty + (th - d) / 2f, d, d);
            TextRenderer.DrawText(g, chipText, chipFont,
                Gfx.Rect(chipX + this.Px(14), ty, chipSize.Width + 2, th), Theme.Muted,
                OneLine | TextFormatFlags.VerticalCenter);
        }

        // Deskripsi (maksimal dua baris)
        TextRenderer.DrawText(g, Category.Description, Theme.Text(9f),
            Gfx.Rect(textX, this.Px(38), availW, this.Px(32)), Theme.Muted,
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
        Color sizeColor = _state == RowState.Cleaning ? Theme.Accent : hasSize ? Theme.Ink : Theme.Muted;
        var sizeFont = hasSize ? Theme.Display(13f) : Theme.Text(10f);
        TextRenderer.DrawText(g, sizeText, sizeFont, Gfx.Rect(rightX, this.Px(12), rightW, this.Px(26)),
            sizeColor, OneLine | TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        // Catatan
        string? note = _note ?? (Locked ? "Butuh administrator" : !Category.SizeKnown ? "Ukuran dihitung Windows" : null);
        if (note != null)
        {
            TextRenderer.DrawText(g, note, Theme.Text(9f), Gfx.Rect(rightX, this.Px(40), rightW, this.Px(18)),
                _noteColor ?? Theme.Muted, OneLine | TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        // Porsi dibanding kategori terbesar
        if (hasSize && Category.SizeKnown)
        {
            float barW = this.Px(120), barH = this.Px(4);
            float bx = w - pad - barW, by = this.Px(62);
            var trackRect = new RectangleF(bx, by, barW, barH);
            using (var tb = new SolidBrush(Theme.Line)) Gfx.FillRound(g, tb, trackRect, barH / 2f);
            float fw = Math.Max(barH, (float)(barW * Math.Clamp(_share, 0, 1)));
            using var fb = new SolidBrush(_checked && can ? Theme.Amber : Theme.Slate);
            Gfx.FillRound(g, fb, new RectangleF(bx, by, fw, barH), barH / 2f);
        }

        // Pemisah
        using (var line = new Pen(Theme.Line, 1f))
            g.DrawLine(line, pad, h - 1, w - pad, h - 1);

        // Fokus keyboard
        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Theme.Accent, this.Px(2f));
            Gfx.DrawRound(g, ring, new RectangleF(this.Px(10), this.Px(3), w - this.Px(20), h - this.Px(7)), this.Px(8));
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            e.Handled = true;
            Toggle();
        }
        base.OnKeyDown(e);
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
