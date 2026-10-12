using AutoInvoicing.Core.Data;
using AutoInvoicing.Core.Models;
using AutoInvoicing.Core.Services;

namespace AutoInvoicing.Tests;

public class ReportTests : IDisposable
{
    private readonly TestDatabase _t = new();
    private readonly InvoiceService _service;
    private readonly InvoiceRepository _invoices;

    public ReportTests()
    {
        _service = new InvoiceService(_t.Db);
        _invoices = new InvoiceRepository(_t.Db);
    }

    public void Dispose() => _t.Dispose();

    private Invoice Finalized(DateTime date, decimal taxRate, params LineItem[] lines)
    {
        var inv = _service.NewDraft(_t.Customer.Id, _t.Vehicle.Id);
        inv.InvoiceDate = date;
        inv.TaxRatePercent = taxRate;
        inv.Lines.AddRange(lines);
        _invoices.SaveDraft(inv);
        _service.Finalize(inv.Id);
        return inv;
    }

    [Theory]
    [InlineData("2026-10-12", "2026-10-12", "2026-10-18")] // Monday
    [InlineData("2026-10-15", "2026-10-12", "2026-10-18")] // Thursday
    [InlineData("2026-10-18", "2026-10-12", "2026-10-18")] // Sunday belongs to the week before
    public void Week_runs_monday_to_sunday(string day, string start, string end)
    {
        var p = ReportPeriod.For(PeriodKind.Weekly, DateTime.Parse(day));

        Assert.Equal(DateTime.Parse(start), p.Start);
        Assert.Equal(DateTime.Parse(end), p.End);
    }

    [Fact]
    public void Month_covers_first_to_last_day_including_leap_february()
    {
        var p = ReportPeriod.For(PeriodKind.Monthly, new DateTime(2028, 2, 14));

        Assert.Equal(new DateTime(2028, 2, 1), p.Start);
        Assert.Equal(new DateTime(2028, 2, 29), p.End);
    }

    [Fact]
    public void Daily_report_sums_only_that_days_finalized_invoices()
    {
        var day = new DateTime(2026, 10, 12);
        var part = _t.AddPart("OF-1", qty: 10m, cost: 4m, price: 10m);
        Finalized(day, 10m, TestDatabase.PartLine(part, 2m), TestDatabase.Labor("Diag", 1m, 120m));
        Finalized(day.AddDays(1), 10m, TestDatabase.Labor("Other day", 1m, 500m));

        var draft = _service.NewDraft(_t.Customer.Id, _t.Vehicle.Id);
        draft.InvoiceDate = day;
        draft.Lines.Add(TestDatabase.Labor("Still a draft", 1m, 999m));
        _invoices.SaveDraft(draft);

        var voided = Finalized(day, 10m, TestDatabase.Labor("Voided", 1m, 777m));
        _service.Void(voided.Id, "test");

        var r = new ReportService(_t.Db).Build(PeriodKind.Daily, day);

        Assert.Equal(1, r.InvoiceCount);
        Assert.Equal(120m, r.Labor);
        Assert.Equal(20m, r.Parts);
        Assert.Equal(140m, r.Subtotal);
        Assert.Equal(2.00m, r.Tax);
        Assert.Equal(142.00m, r.Total);
        Assert.Equal(8m, r.PartsCost);
        Assert.Equal(12m, r.PartsMargin);
        Assert.Equal(132m, r.GrossProfit);
    }

    [Fact]
    public void Collected_uses_payment_date_not_invoice_date()
    {
        var monday = new DateTime(2026, 10, 12);
        var inv = Finalized(monday.AddDays(-7), 0m, TestDatabase.Labor("Last week job", 1m, 300m));
        _service.AddPayment(inv.Id, 100m, "Card", monday.AddDays(2));
        _service.AddPayment(inv.Id, 50m, "Cash", monday.AddDays(9));

        var week = new ReportService(_t.Db).Build(PeriodKind.Weekly, monday);

        Assert.Equal(0, week.InvoiceCount);
        Assert.Equal(100m, week.Collected);
        Assert.Equal(100m, week.CollectedByMethod["Card"]);
        Assert.Equal(7, week.ByDay.Count);
    }

    [Fact]
    public void Monthly_report_totals_match_sum_of_invoice_totals()
    {
        var oct = new DateTime(2026, 10, 1);
        var part = _t.AddPart("RTR-1", qty: 50m, cost: 31.17m, price: 64.99m);
        var expected = 0m;
        for (var i = 0; i < 20; i++)
        {
            var inv = Finalized(oct.AddDays(i), 6.625m,
                TestDatabase.PartLine(part, 2m), TestDatabase.Labor("Brakes", 1.3m, 129.95m));
            expected += _invoices.Get(inv.Id)!.Totals.Total;
        }

        var r = new ReportService(_t.Db).Build(PeriodKind.Monthly, oct.AddDays(15));

        Assert.Equal(20, r.InvoiceCount);
        Assert.Equal(expected, r.Total);
        Assert.Equal(r.Subtotal + r.Tax, r.Total);
        Assert.Equal(40m, r.TopParts.Single().Quantity);
        Assert.Equal(10m, new PartRepository(_t.Db).Get(part.Id)!.QuantityOnHand);
    }
}
