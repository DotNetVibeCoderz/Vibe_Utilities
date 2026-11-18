using System.Drawing.Drawing2D;

namespace Lega.UI;

public enum ButtonKind { Primary, Secondary, Quiet }

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

    public int MeasureWidth() =>
        TextRenderer.MeasureText(Text, Font, Size.Empty, Flags).Width + (int)this.Px(36);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        float radius = this.Px(8);

        Color fill, text, border = Color.Empty;
        switch (Kind)
        {
            case ButtonKind.Primary:
                fill = !Enabled ? Theme.Track : _down ? Theme.AccentDown : _hover ? Theme.AccentHover : Theme.Accent;
                text = !Enabled ? Theme.Muted : Color.White;
                break;
            case ButtonKind.Secondary:
                fill = !Enabled ? Theme.Mist : _down ? Theme.Line : _hover ? Theme.Hover : Theme.Surface;
                border = Theme.Edge;
                text = !Enabled ? Theme.Muted : Theme.Ink;
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

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Theme.Accent, this.Px(2f));
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

/// <summary>Garis tipis 2 px: pembatas saat diam, penanda aktivitas saat memindai atau membersihkan.</summary>
public sealed class SlimProgress : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private float _pos;
    private bool _active;

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
        g.Clear(Theme.Line);
        if (!_active) return;

        int seg = (int)(Width * 0.28f);
        float p = SystemInformation.UIEffectsEnabled ? _pos : 0.4f;
        int x = (int)(-seg + (Width + seg) * p);
        using var b = new SolidBrush(Theme.Accent);
        g.FillRectangle(b, x, 0, seg, Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
