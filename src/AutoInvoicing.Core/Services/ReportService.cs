using AutoInvoicing.Core.Data;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.Core.Services;

public sealed record PartSalesRow(string PartNumber, string Description, decimal Quantity, decimal Sales, decimal Cost)
{
    public decimal Margin => Sales - Cost;
}

public sealed record DailyRow(DateTime Date, int InvoiceCount, decimal Sales, decimal Collected);

public sealed class SalesReport
{
    public required ReportPeriod Period { get; init; }
    public int InvoiceCount { get; init; }
    public decimal Labor { get; init; }
    public decimal Parts { get; init; }
    public decimal Fees { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal Total { get; init; }
    public decimal PartsCost { get; init; }
    public decimal PartsMargin => Parts - PartsCost;
    public decimal GrossProfit => Subtotal - PartsCost;
    public decimal AverageTicket => InvoiceCount == 0 ? 0m : Money.Round(Subtotal / InvoiceCount);
    public decimal Collected { get; init; }
    public decimal OutstandingFromPeriod { get; init; }
    public IReadOnlyDictionary<string, decimal> CollectedByMethod { get; init; } = new Dictionary<string, decimal>();
    public IReadOnlyList<DailyRow> ByDay { get; init; } = [];
    public IReadOnlyList<PartSalesRow> TopParts { get; init; } = [];
    public IReadOnlyList<InvoiceSummary> Invoices { get; init; } = [];
}

/// <summary>
/// Sales come from finalized invoices dated in the period; money collected comes
/// from payments dated in the period (which may be on invoices from earlier).
/// </summary>
public sealed class ReportService(Database db)
{
    private readonly InvoiceRepository _invoices = new(db);

    public SalesReport Build(PeriodKind kind, DateTime anyDay) => Build(ReportPeriod.For(kind, anyDay));

    public SalesReport Build(ReportPeriod period)
    {
        var invoices = _invoices.FinalizedBetween(period.Start, period.End);
        var payments = _invoices.PaymentsBetween(period.Start, period.End);
        var totals = invoices.Select(i => (Invoice: i, Totals: i.Totals)).ToList();

        var byDay = new List<DailyRow>();
        if (period.Kind != PeriodKind.Daily)
        {
            for (var d = period.Start; d <= period.End; d = d.AddDays(1))
            {
                var day = totals.Where(t => t.Invoice.InvoiceDate.Date == d).ToList();
                byDay.Add(new DailyRow(d, day.Count, day.Sum(t => t.Totals.Total),
                    payments.Where(p => p.Date.Date == d).Sum(p => p.Amount)));
            }
        }

        var topParts = invoices
            .SelectMany(i => i.Lines)
            .Where(l => l.Type == LineType.Part)
            .GroupBy(l => (l.PartNumber.Length > 0 ? l.PartNumber : l.Description).ToUpperInvariant())
            .Select(g => new PartSalesRow(
                g.First().PartNumber, g.First().Description, g.Sum(l => l.Quantity),
                g.Sum(l => l.Total), g.Sum(l => Money.Round(l.Quantity * l.UnitCost))))
            .OrderByDescending(r => r.Sales)
            .Take(15)
            .ToList();

        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
        var summaries = _invoices.List(period.Start, period.End)
            .Where(s => invoiceIds.Contains(s.Id))
            .OrderBy(s => s.Number)
            .ToList();

        return new SalesReport
        {
            Period = period,
            InvoiceCount = invoices.Count,
            Labor = totals.Sum(t => t.Totals.LaborTotal),
            Parts = totals.Sum(t => t.Totals.PartsTotal),
            Fees = totals.Sum(t => t.Totals.FeesTotal),
            Subtotal = totals.Sum(t => t.Totals.Subtotal),
            Tax = totals.Sum(t => t.Totals.Tax),
            Total = totals.Sum(t => t.Totals.Total),
            PartsCost = totals.Sum(t => t.Totals.PartsCost),
            Collected = payments.Sum(p => p.Amount),
            OutstandingFromPeriod = totals.Sum(t => t.Totals.BalanceDue),
            CollectedByMethod = payments
                .GroupBy(p => p.Method.Length > 0 ? p.Method : "Other")
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount)),
            ByDay = byDay,
            TopParts = topParts,
            Invoices = summaries,
        };
    }
}
