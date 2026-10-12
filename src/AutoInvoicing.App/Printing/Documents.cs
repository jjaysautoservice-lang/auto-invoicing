using AutoInvoicing.Core;
using AutoInvoicing.Core.Models;
using AutoInvoicing.Core.Services;

namespace AutoInvoicing.App.Printing;

/// <summary>Builds the printable invoice, job ticket, report and list layouts.</summary>
internal static class Documents
{
    private static readonly StringAlignment Right = StringAlignment.Far;
    private static readonly StringAlignment Center = StringAlignment.Center;

    public static BlockDocument Invoice(Invoice inv, Customer customer, Vehicle vehicle, ShopSettings shop)
    {
        var (title, watermark) = inv.Status switch
        {
            InvoiceStatus.Draft => ("ESTIMATE", (string?)null),
            InvoiceStatus.Void => ("INVOICE", "VOID"),
            _ => ("INVOICE", null),
        };
        var t = inv.Totals;
        var blocks = new List<PrintBlock>
        {
            ShopHeader(shop, title, $"{(inv.Status == InvoiceStatus.Draft ? "Estimate" : "Invoice")} #: {inv.Number}\nDate: {inv.InvoiceDate:d}"),
            new SpacerBlock(10),
            new ColumnsBlock(
                (0.5f, new BoxBlock("Bill To", CustomerText(customer), 80)),
                (0.5f, new BoxBlock("Vehicle", VehicleText(vehicle, inv.Mileage), 80))),
            new SpacerBlock(12),
        };

        var cols = new[]
        {
            new TableColumn("Type", 9), new TableColumn("Part #", 15), new TableColumn("Description", 42),
            new TableColumn("Qty/Hrs", 10, Right), new TableColumn("Price", 12, Right), new TableColumn("Amount", 12, Right),
        };
        blocks.AddRange(TableRowBlock.Table(cols, inv.Lines.Select(l => (IReadOnlyList<string>)new[]
        {
            l.Type.ToString(), l.PartNumber, l.Description + (l.Taxable ? "" : " *"),
            Ui.Qty(l.Quantity), Ui.Money(l.UnitPrice), Ui.Money(l.Total),
        })));
        blocks.Add(new SpacerBlock(8));

        var totalRows = new List<(string, string, bool)>
        {
            ("Labor", Ui.Money(t.LaborTotal), false),
            ("Parts", Ui.Money(t.PartsTotal), false),
        };
        if (t.FeesTotal != 0m)
            totalRows.Add(("Fees / Sublet", Ui.Money(t.FeesTotal), false));
        totalRows.Add(("Subtotal", Ui.Money(t.Subtotal), false));
        totalRows.Add(($"Sales tax ({inv.TaxRatePercent:0.###}% of {Ui.Money(t.TaxableAmount)})", Ui.Money(t.Tax), false));
        totalRows.Add(("TOTAL", Ui.Money(t.Total), true));
        if (inv.Status != InvoiceStatus.Draft)
        {
            foreach (var p in inv.Payments)
                totalRows.Add(($"Paid {p.Date:d} {p.Method}", "-" + Ui.Money(p.Amount), false));
            totalRows.Add(("BALANCE DUE", Ui.Money(inv.Status == InvoiceStatus.Void ? 0m : t.BalanceDue), true));
        }

        var notes = inv.Notes;
        if (inv.Lines.Any(l => !l.Taxable))
            notes += (notes.Length > 0 ? "\n\n" : "") + "* Not taxable";
        if (inv.Status == InvoiceStatus.Void && inv.VoidReason.Length > 0)
            notes += $"\n\nVOIDED: {inv.VoidReason}";
        blocks.Add(new ColumnsBlock(
            (0.55f, new TextBlock(notes, PrintFonts.Body)),
            (0.45f, new TotalsBlock(totalRows, 1f))));

        blocks.Add(new SpacerBlock(20));
        if (inv.Status == InvoiceStatus.Draft)
            blocks.Add(new TextBlock("This is an estimate. Final charges may vary. I authorize the above repairs.", PrintFonts.Small));
        else if (shop.InvoiceFooter.Length > 0)
            blocks.Add(new TextBlock(shop.InvoiceFooter, PrintFonts.Small, Center));
        blocks.Add(new SignatureBlock("Customer signature", "Date"));

        return new BlockDocument($"Invoice {inv.Number}", blocks, $"{title} #{inv.Number}", watermark);
    }

    /// <summary>Shop work order for the technician: concern, cause, correction, parts and labor, no prices.</summary>
    public static BlockDocument JobTicket(JobTicket ticket, Customer customer, Vehicle vehicle, ShopSettings shop)
    {
        var blocks = new List<PrintBlock>
        {
            ShopHeader(shop, "JOB TICKET",
                $"Ticket #: {ticket.Number}\nOpened: {ticket.DateOpened:d}" +
                (ticket.Promised is { } p ? $"\nPromised: {p:d}" : "") +
                $"\nStatus: {ticket.Status}"),
            new SpacerBlock(10),
            new ColumnsBlock(
                (0.5f, new BoxBlock("Customer", CustomerText(customer), 80)),
                (0.5f, new BoxBlock("Vehicle", VehicleText(vehicle, ticket.Mileage), 80))),
            new SpacerBlock(8),
            new TextBlock("Technician: " + (ticket.Technician.Length > 0 ? ticket.Technician : "____________________"), PrintFonts.BodyBold),
            new SpacerBlock(8),
            new BoxBlock("Customer Concern", ticket.Complaint, 60),
            new SpacerBlock(6),
            new BoxBlock("Cause / Diagnosis", ticket.Cause, 70),
            new SpacerBlock(6),
            new BoxBlock("Correction / Work Performed", ticket.Correction, 70),
            new SpacerBlock(12),
        };

        var cols = new[]
        {
            new TableColumn("Done", 7, Center), new TableColumn("Type", 10), new TableColumn("Part #", 18),
            new TableColumn("Description", 50), new TableColumn("Qty/Hrs", 12, Right),
        };
        var rows = ticket.Lines.Select(l => (IReadOnlyList<string>)new[] { "[  ]", l.Type.ToString(), l.PartNumber, l.Description, Ui.Qty(l.Quantity) }).ToList();
        for (var i = rows.Count; i < 8; i++)
            rows.Add(new[] { "[  ]", "", "", "", "" }); // blank lines for handwritten additions
        blocks.AddRange(TableRowBlock.Table(cols, rows));

        blocks.Add(new SpacerBlock(10));
        if (ticket.Notes.Length > 0)
            blocks.Add(new BoxBlock("Notes", ticket.Notes));
        blocks.Add(new SpacerBlock(10));
        blocks.Add(new SignatureBlock("Technician", "Hours", "Customer authorization"));

        return new BlockDocument($"Job Ticket {ticket.Number}", blocks, $"JOB TICKET #{ticket.Number}");
    }

    public static BlockDocument SalesReport(SalesReport r, ShopSettings shop)
    {
        var blocks = new List<PrintBlock>
        {
            new TextBlock(shop.ShopName, PrintFonts.ShopName),
            new TextBlock(r.Period.Title, PrintFonts.Title),
            new TextBlock($"Printed {DateTime.Now:g}", PrintFonts.Small),
            new RuleBlock(),
            new ColumnsBlock(
                (0.5f, new TotalsBlock(new List<(string, string, bool)>
                {
                    ("Invoices", r.InvoiceCount.ToString(), false),
                    ("Labor", Ui.Money(r.Labor), false),
                    ("Parts", Ui.Money(r.Parts), false),
                    ("Fees / Sublet", Ui.Money(r.Fees), false),
                    ("Subtotal", Ui.Money(r.Subtotal), false),
                    ("Sales tax", Ui.Money(r.Tax), false),
                    ("Total sales", Ui.Money(r.Total), true),
                }, 1f)),
                (0.5f, new TotalsBlock(new List<(string, string, bool)>
                {
                    ("Parts cost", Ui.Money(r.PartsCost), false),
                    ("Parts margin", Ui.Money(r.PartsMargin), false),
                    ("Gross profit (excl. tax)", Ui.Money(r.GrossProfit), false),
                    ("Average ticket", Ui.Money(r.AverageTicket), false),
                    ("Unpaid from these invoices", Ui.Money(r.OutstandingFromPeriod), false),
                    ("Collected", Ui.Money(r.Collected), true),
                }.Concat(r.CollectedByMethod.Select(kv => ($"   {kv.Key}", Ui.Money(kv.Value), false))).ToList(), 1f))),
            new SpacerBlock(14),
        };

        if (r.ByDay.Count > 0)
        {
            blocks.Add(new TextBlock("By Day", PrintFonts.Heading) { KeepWithNext = true });
            blocks.AddRange(TableRowBlock.Table(
                new[] { new TableColumn("Date", 40), new TableColumn("Invoices", 15, Right), new TableColumn("Sales", 22, Right), new TableColumn("Collected", 22, Right) },
                r.ByDay.Select(d => (IReadOnlyList<string>)new[] { d.Date.ToString("ddd MMM d"), d.InvoiceCount.ToString(), Ui.Money(d.Sales), Ui.Money(d.Collected) })));
            blocks.Add(new SpacerBlock(14));
        }

        if (r.TopParts.Count > 0)
        {
            blocks.Add(new TextBlock("Top Parts Sold", PrintFonts.Heading) { KeepWithNext = true });
            blocks.AddRange(TableRowBlock.Table(
                new[] { new TableColumn("Part #", 18), new TableColumn("Description", 40), new TableColumn("Qty", 10, Right), new TableColumn("Sales", 16, Right), new TableColumn("Margin", 16, Right) },
                r.TopParts.Select(p => (IReadOnlyList<string>)new[] { p.PartNumber, p.Description, Ui.Qty(p.Quantity), Ui.Money(p.Sales), Ui.Money(p.Margin) })));
            blocks.Add(new SpacerBlock(14));
        }

        blocks.Add(new TextBlock("Invoices", PrintFonts.Heading) { KeepWithNext = true });
        blocks.AddRange(TableRowBlock.Table(
            new[] { new TableColumn("#", 9), new TableColumn("Date", 12), new TableColumn("Customer", 25), new TableColumn("Vehicle", 26), new TableColumn("Total", 14, Right), new TableColumn("Balance", 14, Right) },
            r.Invoices.Select(i => (IReadOnlyList<string>)new[] { i.Number.ToString(), i.InvoiceDate.ToString("d"), i.CustomerName, i.VehicleDescription, Ui.Money(i.Total), Ui.Money(i.BalanceDue) })));

        return new BlockDocument(r.Period.Title, blocks, r.Period.Title);
    }

    /// <summary>A simple titled table, used for customer/vehicle history and inventory lists.</summary>
    public static BlockDocument Table(string title, string subtitle, IReadOnlyList<TableColumn> columns,
        IEnumerable<IReadOnlyList<string>> rows, ShopSettings shop, string? footer = null)
    {
        var blocks = new List<PrintBlock>
        {
            new TextBlock(shop.ShopName, PrintFonts.ShopName),
            new TextBlock(title, PrintFonts.Title),
            new TextBlock(subtitle, PrintFonts.Body),
            new TextBlock($"Printed {DateTime.Now:g}", PrintFonts.Small),
            new RuleBlock(),
        };
        blocks.AddRange(TableRowBlock.Table(columns, rows));
        if (footer != null)
        {
            blocks.Add(new SpacerBlock(8));
            blocks.Add(new TextBlock(footer, PrintFonts.BodyBold, Right));
        }
        return new BlockDocument(title, blocks, title);
    }

    private static PrintBlock ShopHeader(ShopSettings shop, string title, string docInfo)
    {
        var shopLines = string.Join("\n", new[] { shop.Address, shop.Phone, shop.Email, shop.TaxId.Length > 0 ? "Tax ID: " + shop.TaxId : "" }
            .Where(s => s.Length > 0));
        return new ColumnsBlock(
            (0.6f, new StackBlock(new TextBlock(shop.ShopName, PrintFonts.ShopName), new TextBlock(shopLines, PrintFonts.Body))),
            (0.4f, new StackBlock(new TextBlock(title, PrintFonts.Title, Right), new TextBlock(docInfo, PrintFonts.Body, Right))));
    }

    private static string CustomerText(Customer c) =>
        string.Join("\n", new[] { c.Name, c.Address, c.Phone, c.Email }.Where(s => s.Length > 0));

    private static string VehicleText(Vehicle v, int? mileage) =>
        string.Join("\n", new[]
        {
            v.Description + (v.Color.Length > 0 ? $", {v.Color}" : ""),
            v.Engine.Length > 0 ? "Engine: " + v.Engine : "",
            v.Vin.Length > 0 ? "VIN: " + v.Vin : "",
            v.Plate.Length > 0 ? "Plate: " + v.Plate : "",
            mileage is int m ? $"Mileage: {m:N0}" : "",
        }.Where(s => s.Length > 0));
}

/// <summary>Several blocks stacked as one unit (kept together on a page).</summary>
internal sealed class StackBlock(params PrintBlock[] blocks) : PrintBlock
{
    public override float Measure(Graphics g, float width) => blocks.Sum(b => b.Measure(g, width));

    public override void Draw(Graphics g, RectangleF area)
    {
        var y = area.Top;
        foreach (var b in blocks)
        {
            var h = b.Measure(g, area.Width);
            b.Draw(g, new RectangleF(area.Left, y, area.Width, h));
            y += h;
        }
    }
}
