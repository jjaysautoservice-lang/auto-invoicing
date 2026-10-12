using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AutoInvoicing.App;

/// <summary>Colors and painted controls for the card-style dialogs (e.g. the customer form).</summary>
internal static class Styled
{
    public static readonly Color HeaderBack = Color.FromArgb(27, 74, 122);
    public static readonly Color HeaderCaption = Color.FromArgb(169, 200, 236);
    public static readonly Color SectionBack = Color.FromArgb(244, 242, 236);
    public static readonly Color SectionBorder = Color.FromArgb(218, 214, 204);
    public static readonly Color SectionCaption = Color.FromArgb(110, 110, 110);
    public static readonly Color InputBorder = Color.FromArgb(200, 200, 200);
    public static readonly Color InputFocus = Color.FromArgb(27, 110, 194);
    public static readonly Color Required = Color.FromArgb(255, 242, 204);
    public static readonly Color SaveBack = Color.FromArgb(46, 125, 50);
    public static readonly Color Border = Color.FromArgb(120, 140, 160);

    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Paints a rounded box over the parent's background, so the corners blend in.</summary>
    public static void PaintRounded(Control c, Graphics g, Color fill, Color border, int radius)
    {
        g.Clear(c.Parent?.BackColor ?? SystemColors.Control);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundRect(new Rectangle(0, 0, c.Width - 1, c.Height - 1), radius);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border);
        g.FillPath(brush, path);
        g.DrawPath(pen, path);
    }

    public static Label Caption(string text, int x, int y, Font font, Color? color = null) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color ?? Color.Black,
        AutoSize = true,
        Location = new Point(x, y),
        BackColor = Color.Transparent,
    };

    public static Button Button(string text, Color back, Color fore, Color border)
    {
        var b = new Button
        {
            Text = text,
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = border;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.15f);
        return b;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Lets the user drag a borderless form around by this control.</summary>
    public static void DragsForm(this Control c, Form form)
    {
        c.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;
            ReleaseCapture();
            SendMessage(form.Handle, 0xA1 /* WM_NCLBUTTONDOWN */, 2 /* HTCAPTION */, IntPtr.Zero);
        };
    }
}

/// <summary>Light rounded panel with a small uppercase caption, used to group related fields.</summary>
internal sealed class SectionPanel : Panel
{
    public SectionPanel(string caption)
    {
        DoubleBuffered = true;
        BackColor = Styled.SectionBack;
        Controls.Add(Styled.Caption(caption.ToUpperInvariant(), 18, 8, new Font("Segoe UI", 8.5f, FontStyle.Bold), Styled.SectionCaption));
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        Styled.PaintRounded(this, e.Graphics, Styled.SectionBack, Styled.SectionBorder, 6);
}

/// <summary>
/// Tall rounded text box: a borderless TextBox centered in a painted frame. When
/// <see cref="Required"/> is set the box is shaded yellow until something is typed.
/// </summary>
internal sealed class InputBox : Panel
{
    public TextBox Box { get; } = new() { BorderStyle = BorderStyle.None };
    public bool Required { get; init; }

    public InputBox(string value = "", int maxLength = 0)
    {
        DoubleBuffered = true;
        Height = 34;
        Box.Text = value;
        if (maxLength > 0)
            Box.MaxLength = maxLength;
        Controls.Add(Box);
        Click += (_, _) => Box.Focus();
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Box.TextChanged += (_, _) => UpdateFill();
        HandleCreated += (_, _) => UpdateFill();
    }

    public override string Text
    {
        get => Box.Text;
#pragma warning disable CS8765 // matches Control.Text, which never stores null
        set => Box.Text = value;
#pragma warning restore CS8765
    }

    public string Placeholder
    {
        get => Box.PlaceholderText;
        set => Box.PlaceholderText = value;
    }

    private Color Fill => Required && Box.Text.Trim().Length == 0 ? Styled.Required : Color.White;

    private void UpdateFill()
    {
        Box.BackColor = Fill;
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Box.Width = Width - 14;
        Box.Location = new Point(7, (Height - Box.Height) / 2);
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        Styled.PaintRounded(this, e.Graphics, Fill, Box.Focused ? Styled.InputFocus : Styled.InputBorder, 4);
}

/// <summary>Flat drop-down list drawn at the same height as <see cref="InputBox"/>.</summary>
internal sealed class TallComboBox : ComboBox
{
    public TallComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 28;
        BackColor = Color.White;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index >= 0)
        {
            var text = GetItemText(Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, e.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
        e.DrawFocusRectangle();
    }
}
