using System.Drawing.Drawing2D;

namespace Lega.UI;

public enum ButtonKind { Primary, Secondary, Quiet, OnDark }

public sealed class ActionButton : Control
{
    private const TextFormatFlags Flags =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

    private bool _hover, _down;

    public ButtonKind Kind { get; }

    public ActionButton(string text, ButtonKind kind, float fontPt = 10f)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Font = Theme.Semi(fontPt);
        Text = text;
        Kind = kind;
        AccessibleRole = AccessibleRole.PushButton;
    }

    /// <summary>Ikon Segoe Fluent Icons di kiri teks (opsional).</summary>
    public string? Glyph { get; init; }

    private const TextFormatFlags Measure = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    private float GlyphSlot => Glyph is null ? 0 : this.Px(16) + this.Px(8);

    public int MeasureWidth() => MeasureText() + (int)(this.Px(40) + GlyphSlot);

    private int MeasureText()
    {
        if (!IsHandleCreated) return TextRenderer.MeasureText(Text, Font, Size.Empty, Measure).Width;
        using var g = CreateGraphics();
        return TextRenderer.MeasureText(g, Text, Font, Size.Empty, Measure).Width;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        float radius = this.Px(6);

        Color fill, text, border = Color.Empty;
        switch (Kind)
        {
            case ButtonKind.Primary:
                fill = !Enabled ? Theme.Track : _down ? Theme.AccentDown : _hover ? Theme.AccentHover : Theme.Accent;
                text = !Enabled ? Theme.Muted : Color.White;
                break;
            case ButtonKind.Secondary:
                fill = !Enabled ? Theme.Paper : _down ? Theme.Line : _hover ? Theme.Hover : Theme.Surface;
                border = Theme.Edge;
                text = !Enabled ? Theme.Muted : Theme.Ink;
                break;
            case ButtonKind.OnDark:
                // Kunyit = ruang yang bisa dikosongkan; tombol ini membuka lebih banyak lagi.
                fill = _down ? Theme.PineDeep : _hover && Enabled ? Theme.PineHover : BackColor;
                border = Enabled ? Theme.Turmeric : Theme.PineLine;
                text = !Enabled ? Theme.OnPineMuted : Theme.Turmeric;
                break;
            default:
                fill = _down ? Theme.Line : _hover && Enabled ? Theme.Surface : BackColor;
                text = !Enabled ? Theme.Muted : Theme.AccentHover;
                break;
        }

        using (var b = new SolidBrush(fill)) Gfx.FillRound(g, b, r, radius);
        if (border != Color.Empty)
        {
            using var pen = new Pen(border, 1f);
            Gfx.DrawRound(g, pen, r, radius);
        }

        if (Glyph is null)
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
                Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else
        {
            // Ikon + teks dipusatkan sebagai satu kelompok.
            int tw = TextRenderer.MeasureText(g, Text, Font, Size.Empty, Measure).Width;
            float icon = this.Px(16), gap = this.Px(8);
            float x = Math.Max(this.Px(12), (Width - (icon + gap + tw)) / 2f);
            TextRenderer.DrawText(g, Glyph, Theme.Icon(Font.SizeInPoints), Gfx.Rect(x, 0, icon, Height), text,
                Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Text, Font, Gfx.Rect(x + icon + gap, 0, Width - (x + icon + gap) - this.Px(8), Height), text,
                Flags | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Kind == ButtonKind.OnDark ? Theme.Turmeric : Kind == ButtonKind.Primary ? Color.White : Theme.Accent, this.Px(2f));
            Gfx.DrawRound(g, ring, RectangleF.Inflate(r, -this.Px(3), -this.Px(3)), radius - 2);
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }

    protected override void OnClick(EventArgs e)
    {
        if (Enabled) base.OnClick(e);
    }
}

/// <summary>Garis tipis di tepi bawah panel instrumen: diam saat siap, berjalan saat memindai atau membersihkan.</summary>
public sealed class SlimProgress : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private float _pos;
    private bool _active;

    public Color TrackColor { get; set; } = Theme.Line;
    public Color RunnerColor { get; set; } = Theme.Accent;

    public SlimProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        _timer.Tick += (_, _) =>
        {
            _pos += 0.011f;
            if (_pos > 1f) _pos = 0f;
            Invalidate();
        };
    }

    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            if (value && SystemInformation.UIEffectsEnabled) _timer.Start(); else _timer.Stop();
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(TrackColor);
        if (!_active) return;

        int seg = (int)(Width * 0.28f);
        float p = SystemInformation.UIEffectsEnabled ? _pos : 0.4f;
        int x = (int)(-seg + (Width + seg) * p);
        using var b = new SolidBrush(RunnerColor);
        g.FillRectangle(b, x, 0, seg, Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
