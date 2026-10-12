using System.Drawing.Printing;

namespace AutoInvoicing.App.Printing;

internal static class PrintFonts
{
    public const string Family = "Arial";
    public static readonly Font Title = new(Family, 18f, FontStyle.Bold);
    public static readonly Font ShopName = new(Family, 15f, FontStyle.Bold);
    public static readonly Font Heading = new(Family, 10.5f, FontStyle.Bold);
    public static readonly Font Body = new(Family, 9.5f);
    public static readonly Font BodyBold = new(Family, 9.5f, FontStyle.Bold);
    public static readonly Font Small = new(Family, 8f);
    public static readonly Font Total = new(Family, 12f, FontStyle.Bold);
    public static readonly Font Watermark = new(Family, 72f, FontStyle.Bold);
}

/// <summary>A piece of a printed page. Blocks are stacked top to bottom and split across pages.</summary>
internal abstract class PrintBlock
{
    /// <summary>Printed again at the top of a new page before this block (table headers).</summary>
    public PrintBlock? RepeatHeader { get; init; }

    /// <summary>Don't leave this block alone at the bottom of a page.</summary>
    public bool KeepWithNext { get; init; }

    public abstract float Measure(Graphics g, float width);
    public abstract void Draw(Graphics g, RectangleF area);
}

internal sealed class SpacerBlock(float height) : PrintBlock
{
    public override float Measure(Graphics g, float width) => height;
    public override void Draw(Graphics g, RectangleF area) { }
}

internal sealed class RuleBlock(float thickness = 1f) : PrintBlock
{
    public override float Measure(Graphics g, float width) => thickness + 6f;

    public override void Draw(Graphics g, RectangleF area)
    {
        using var pen = new Pen(Color.Black, thickness);
        var y = area.Top + 3f;
        g.DrawLine(pen, area.Left, y, area.Right, y);
    }
}

internal sealed class TextBlock(string text, Font font, StringAlignment align = StringAlignment.Near) : PrintBlock
{
    private StringFormat Format => new() { Alignment = align, Trimming = StringTrimming.Word };

    public override float Measure(Graphics g, float width) =>
        text.Length == 0 ? 0f : g.MeasureString(text, font, (int)width, Format).Height;

    public override void Draw(Graphics g, RectangleF area) => g.DrawString(text, font, Brushes.Black, area, Format);
}

/// <summary>Places blocks next to each other; widths are fractions of the page width.</summary>
internal sealed class ColumnsBlock(params (float Fraction, PrintBlock Block)[] columns) : PrintBlock
{
    private const float Gap = 12f;

    private IEnumerable<(PrintBlock Block, float X, float Width)> Layout(float left, float width)
    {
        var usable = width - Gap * (columns.Length - 1);
        var x = left;
        foreach (var (fraction, block) in columns)
        {
            var w = usable * fraction;
            yield return (block, x, w);
            x += w + Gap;
        }
    }

    public override float Measure(Graphics g, float width) =>
        Layout(0, width).Max(c => c.Block.Measure(g, c.Width));

    public override void Draw(Graphics g, RectangleF area)
    {
        foreach (var (block, x, w) in Layout(area.Left, area.Width))
            block.Draw(g, new RectangleF(x, area.Top, w, area.Height)); // same height so boxes line up
    }
}

/// <summary>A titled box, used for Bill To, Vehicle and similar panels.</summary>
internal sealed class BoxBlock(string title, string body, float minHeight = 0f) : PrintBlock
{
    private const float Pad = 5f;

    public override float Measure(Graphics g, float width)
    {
        var titleH = g.MeasureString(title, PrintFonts.BodyBold).Height;
        var bodyH = body.Length == 0 ? 0 : g.MeasureString(body, PrintFonts.Body, (int)(width - 2 * Pad)).Height;
        return Math.Max(minHeight, titleH + bodyH + 3 * Pad);
    }

    public override void Draw(Graphics g, RectangleF area)
    {
        var titleH = g.MeasureString(title, PrintFonts.BodyBold).Height + Pad;
        g.FillRectangle(Brushes.Gainsboro, area.Left, area.Top, area.Width, titleH);
        g.DrawRectangle(Pens.Black, area.Left, area.Top, area.Width, area.Height);
        g.DrawString(title, PrintFonts.BodyBold, Brushes.Black, area.Left + Pad, area.Top + Pad / 2);
        g.DrawString(body, PrintFonts.Body, Brushes.Black,
            new RectangleF(area.Left + Pad, area.Top + titleH + Pad / 2, area.Width - 2 * Pad, area.Height - titleH));
    }
}

internal sealed record TableColumn(string Header, float Weight, StringAlignment Align = StringAlignment.Near);

/// <summary>One row of a table. Long text wraps inside its cell; the row grows to fit.</summary>
internal sealed class TableRowBlock(IReadOnlyList<TableColumn> columns, IReadOnlyList<string> cells, bool header = false, bool shade = false)
    : PrintBlock
{
    private const float Pad = 3f;
    private Font Font => header ? PrintFonts.BodyBold : PrintFonts.Body;

    public static List<PrintBlock> Table(IReadOnlyList<TableColumn> columns, IEnumerable<IReadOnlyList<string>> rows)
    {
        var head = new TableRowBlock(columns, columns.Select(c => c.Header).ToList(), header: true) { KeepWithNext = true };
        var blocks = new List<PrintBlock> { head };
        var i = 0;
        foreach (var row in rows)
            blocks.Add(new TableRowBlock(columns, row, shade: i++ % 2 == 1) { RepeatHeader = head });
        return blocks;
    }

    private float[] Widths(float width)
    {
        var total = columns.Sum(c => c.Weight);
        return columns.Select(c => width * c.Weight / total).ToArray();
    }

    public override float Measure(Graphics g, float width)
    {
        var widths = Widths(width);
        var h = 0f;
        for (var i = 0; i < columns.Count; i++)
            h = Math.Max(h, g.MeasureString(cells[i], Font, (int)Math.Max(1, widths[i] - 2 * Pad)).Height);
        return h + 2 * Pad;
    }

    public override void Draw(Graphics g, RectangleF area)
    {
        if (header)
            g.FillRectangle(Brushes.Gainsboro, area);
        else if (shade)
            g.FillRectangle(Brushes.WhiteSmoke, area);

        var widths = Widths(area.Width);
        var x = area.Left;
        for (var i = 0; i < columns.Count; i++)
        {
            var fmt = new StringFormat { Alignment = columns[i].Align };
            g.DrawString(cells[i], Font, Brushes.Black, new RectangleF(x + Pad, area.Top + Pad, widths[i] - 2 * Pad, area.Height), fmt);
            x += widths[i];
        }
        g.DrawLine(header ? Pens.Black : Pens.LightGray, area.Left, area.Bottom, area.Right, area.Bottom);
    }
}

/// <summary>Right-aligned label/amount pairs, e.g. invoice totals.</summary>
internal sealed class TotalsBlock(IReadOnlyList<(string Label, string Value, bool Strong)> rows, float widthFraction = 0.4f) : PrintBlock
{
    private const float RowPad = 2f;

    private static Font FontFor(bool strong) => strong ? PrintFonts.Total : PrintFonts.Body;

    public override float Measure(Graphics g, float width) =>
        rows.Sum(r => g.MeasureString(r.Label, FontFor(r.Strong)).Height + RowPad);

    public override void Draw(Graphics g, RectangleF area)
    {
        var boxWidth = area.Width * widthFraction;
        var left = area.Right - boxWidth;
        var y = area.Top;
        var right = new StringFormat { Alignment = StringAlignment.Far };
        foreach (var (label, value, strong) in rows)
        {
            var font = FontFor(strong);
            var h = g.MeasureString(label, font).Height;
            if (strong)
                g.DrawLine(Pens.Black, left, y, area.Right, y);
            g.DrawString(label, font, Brushes.Black, left, y);
            g.DrawString(value, font, Brushes.Black, new RectangleF(left, y, boxWidth, h), right);
            y += h + RowPad;
        }
    }
}

/// <summary>Signature lines, e.g. customer authorization or technician sign-off.</summary>
internal sealed class SignatureBlock(params string[] labels) : PrintBlock
{
    public override float Measure(Graphics g, float width) => 48f;

    public override void Draw(Graphics g, RectangleF area)
    {
        var w = (area.Width - 24f * (labels.Length - 1)) / labels.Length;
        for (var i = 0; i < labels.Length; i++)
        {
            var x = area.Left + i * (w + 24f);
            var y = area.Top + 28f;
            g.DrawLine(Pens.Black, x, y, x + w, y);
            g.DrawString(labels[i], PrintFonts.Small, Brushes.Black, x, y + 2f);
        }
    }
}

/// <summary>
/// A print document built from blocks. Handles page breaks, repeating table headers,
/// a continuation line on later pages, page numbers, and an optional watermark (DRAFT, VOID).
/// </summary>
internal sealed class BlockDocument : PrintDocument
{
    private readonly List<PrintBlock> _blocks;
    private readonly string _continuation;
    private readonly string? _watermark;
    private int _next;
    private int _page;

    public BlockDocument(string documentName, IEnumerable<PrintBlock> blocks, string continuation, string? watermark = null)
    {
        DocumentName = documentName;
        _blocks = blocks.ToList();
        _continuation = continuation;
        _watermark = watermark;
        DefaultPageSettings.Margins = new Margins(50, 50, 50, 50); // half inch
    }

    protected override void OnBeginPrint(PrintEventArgs e)
    {
        _next = 0;
        _page = 0;
        base.OnBeginPrint(e);
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        base.OnPrintPage(e);
        var g = e.Graphics!;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var bounds = (RectangleF)e.MarginBounds;
        _page++;

        if (_watermark != null)
            DrawWatermark(g, bounds);

        var footerH = g.MeasureString("X", PrintFonts.Small).Height + 4f;
        var bottom = bounds.Bottom - footerH;
        var y = bounds.Top;

        if (_page > 1)
        {
            g.DrawString($"{_continuation} (continued)", PrintFonts.BodyBold, Brushes.Black, bounds.Left, y);
            y += g.MeasureString(_continuation, PrintFonts.BodyBold).Height + 8f;
            if (_next < _blocks.Count && _blocks[_next].RepeatHeader is { } header)
                y += DrawBlock(g, header, bounds.Left, y, bounds.Width);
        }

        var firstOnPage = true;
        while (_next < _blocks.Count)
        {
            var block = _blocks[_next];
            var h = block.Measure(g, bounds.Width);
            var needed = h;
            if (block.KeepWithNext && _next + 1 < _blocks.Count)
                needed += _blocks[_next + 1].Measure(g, bounds.Width);

            if (y + needed > bottom && !firstOnPage)
                break;

            block.Draw(g, new RectangleF(bounds.Left, y, bounds.Width, h));
            y += h;
            _next++;
            firstOnPage = false;
        }

        var footer = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString($"Page {_page}", PrintFonts.Small, Brushes.Black, new RectangleF(bounds.Left, bounds.Bottom - footerH + 4f, bounds.Width, footerH), footer);

        e.HasMorePages = _next < _blocks.Count;
    }

    private static float DrawBlock(Graphics g, PrintBlock block, float x, float y, float width)
    {
        var h = block.Measure(g, width);
        block.Draw(g, new RectangleF(x, y, width, h));
        return h;
    }

    private void DrawWatermark(Graphics g, RectangleF bounds)
    {
        var state = g.Save();
        g.TranslateTransform(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        g.RotateTransform(-35f);
        using var brush = new SolidBrush(Color.FromArgb(40, Color.Gray));
        var size = g.MeasureString(_watermark, PrintFonts.Watermark);
        g.DrawString(_watermark, PrintFonts.Watermark, brush, -size.Width / 2, -size.Height / 2);
        g.Restore(state);
    }
}

internal static class PrintHelper
{
    public static void Preview(IWin32Window owner, PrintDocument doc)
    {
        using var dlg = new PrintPreviewDialog
        {
            Document = doc,
            Width = 1000,
            Height = 900,
            StartPosition = FormStartPosition.CenterParent,
            UseAntiAlias = true,
        };
        try
        {
            dlg.ShowDialog(owner);
        }
        catch (InvalidPrinterException)
        {
            Ui.Info("No printer is installed. Add a printer (or \"Microsoft Print to PDF\") in Windows settings to preview and print.");
        }
    }

    public static void Print(IWin32Window owner, PrintDocument doc)
    {
        using var dlg = new PrintDialog { Document = doc, UseEXDialog = true };
        if (dlg.ShowDialog(owner) == DialogResult.OK)
            doc.Print();
    }
}
