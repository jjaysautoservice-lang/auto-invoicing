using AutoInvoicing.Core.Data;
using AutoInvoicing.Core.Models;
using AutoInvoicing.Core.Services;

namespace AutoInvoicing.Tests;

public class InventoryDeductionTests : IDisposable
{
    private readonly TestDatabase _t = new();
    private readonly InvoiceService _service;
    private readonly InvoiceRepository _invoices;
    private readonly PartRepository _parts;

    public InventoryDeductionTests()
    {
        _service = new InvoiceService(_t.Db);
        _invoices = new InvoiceRepository(_t.Db);
        _parts = new PartRepository(_t.Db);
    }

    public void Dispose() => _t.Dispose();

    private Invoice Draft(params LineItem[] lines)
    {
        var inv = _service.NewDraft(_t.Customer.Id, _t.Vehicle.Id);
        inv.Lines.AddRange(lines);
        _invoices.SaveDraft(inv);
        return inv;
    }

    [Fact]
    public void Saving_a_draft_does_not_touch_stock()
    {
        var filter = _t.AddPart("OF-1", qty: 10m);

        Draft(TestDatabase.PartLine(filter, 2m));

        Assert.Equal(10m, _parts.Get(filter.Id)!.QuantityOnHand);
    }

    [Fact]
    public void Finalize_deducts_each_part_from_stock()
    {
        var filter = _t.AddPart("OF-1", qty: 10m);
        var oil = _t.AddPart("5W30-QT", qty: 24m, cost: 3.10m, price: 7.49m);
        var inv = Draft(
            TestDatabase.PartLine(filter, 1m),
            TestDatabase.PartLine(oil, 4.5m),
            TestDatabase.Labor("Oil change", 0.5m, 100m));

        _service.Finalize(inv.Id);

        Assert.Equal(9m, _parts.Get(filter.Id)!.QuantityOnHand);
        Assert.Equal(19.5m, _parts.Get(oil.Id)!.QuantityOnHand);
        Assert.Equal(InvoiceStatus.Finalized, _invoices.Get(inv.Id)!.Status);
    }

    [Fact]
    public void Same_part_on_two_lines_is_deducted_once_for_the_combined_quantity()
    {
        var pad = _t.AddPart("BP-200", qty: 8m);
        var inv = Draft(TestDatabase.PartLine(pad, 1m), TestDatabase.PartLine(pad, 1m));

        _service.Finalize(inv.Id);

        Assert.Equal(6m, _parts.Get(pad.Id)!.QuantityOnHand);
        var tx = Assert.Single(_parts.Transactions(pad.Id), x => x.InvoiceId == inv.Id);
        Assert.Equal(-2m, tx.QuantityChange);
    }

    [Fact]
    public void Non_inventory_part_lines_are_billed_but_not_deducted()
    {
        var stocked = _t.AddPart("SP-4", qty: 4m);
        var specialOrder = new LineItem { Type = LineType.Part, Description = "Dealer-only sensor", Quantity = 1m, UnitPrice = 89m, Taxable = true };
        var inv = Draft(TestDatabase.PartLine(stocked, 4m), specialOrder);

        _service.Finalize(inv.Id);

        Assert.Equal(0m, _parts.Get(stocked.Id)!.QuantityOnHand);
        Assert.Equal(89m + 80m, _invoices.Get(inv.Id)!.Totals.PartsTotal);
    }

    [Fact]
    public void Finalize_twice_is_rejected_and_stock_is_deducted_only_once()
    {
        var filter = _t.AddPart("OF-1", qty: 10m);
        var inv = Draft(TestDatabase.PartLine(filter, 3m));

        _service.Finalize(inv.Id);
        Assert.Throws<InvalidOperationException>(() => _service.Finalize(inv.Id));

        Assert.Equal(7m, _parts.Get(filter.Id)!.QuantityOnHand);
    }

    [Fact]
    public void Finalized_invoice_cannot_be_edited()
    {
        var filter = _t.AddPart("OF-1", qty: 10m);
        var inv = Draft(TestDatabase.PartLine(filter, 1m));
        _service.Finalize(inv.Id);

        inv.Lines[0].Quantity = 5m;

        Assert.Throws<InvalidOperationException>(() => _invoices.SaveDraft(inv));
        Assert.Equal(9m, _parts.Get(filter.Id)!.QuantityOnHand);
    }

    [Fact]
    public void Voiding_a_finalized_invoice_returns_parts_to_stock()
    {
        var filter = _t.AddPart("OF-1", qty: 10m);
        var inv = Draft(TestDatabase.PartLine(filter, 2m));
        _service.Finalize(inv.Id);

        _service.Void(inv.Id, "Customer declined");

        Assert.Equal(10m, _parts.Get(filter.Id)!.QuantityOnHand);
        Assert.Equal(InvoiceStatus.Void, _invoices.Get(inv.Id)!.Status);
    }

    [Fact]
    public void Voiding_a_draft_does_not_change_stock()
    {
        var filter = _t.AddPart("OF-1", qty: 10m);
        var inv = Draft(TestDatabase.PartLine(filter, 2m));

        _service.Void(inv.Id, "Entered by mistake");

        Assert.Equal(10m, _parts.Get(filter.Id)!.QuantityOnHand);
    }

    [Fact]
    public void Selling_more_than_on_hand_goes_negative_and_warns()
    {
        var belt = _t.AddPart("BELT-9", qty: 1m);
        var inv = Draft(TestDatabase.PartLine(belt, 2m));

        var warnings = _service.Finalize(inv.Id);

        Assert.Equal(-1m, _parts.Get(belt.Id)!.QuantityOnHand);
        var w = Assert.Single(warnings);
        Assert.True(w.IsNegative);
    }

    [Fact]
    public void Finalize_warns_when_part_hits_reorder_level()
    {
        var plug = _t.AddPart("SPK-1", qty: 10m, reorder: 4m);
        var wiper = _t.AddPart("WIP-1", qty: 10m, reorder: 2m);
        var inv = Draft(TestDatabase.PartLine(plug, 6m), TestDatabase.PartLine(wiper, 1m));

        var warnings = _service.Finalize(inv.Id);

        var w = Assert.Single(warnings);
        Assert.Equal("SPK-1", w.PartNumber);
        Assert.False(w.IsNegative);
    }

    [Fact]
    public void Every_stock_change_is_logged()
    {
        var filter = _t.AddPart("OF-1", qty: 10m);
        _parts.AdjustStock(filter.Id, 12m, "Received PO 554");
        var inv = Draft(TestDatabase.PartLine(filter, 1m));
        _service.Finalize(inv.Id);

        var log = _parts.Transactions(filter.Id);

        Assert.Equal(new[] { 10m, 12m, -1m }, log.OrderBy(x => x.Id).Select(x => x.QuantityChange));
        Assert.Equal(21m, _parts.Get(filter.Id)!.QuantityOnHand);
        Assert.Equal(log.Sum(x => x.QuantityChange), _parts.Get(filter.Id)!.QuantityOnHand);
    }

    [Fact]
    public void Editing_part_details_does_not_overwrite_on_hand_quantity()
    {
        var filter = _t.AddPart("OF-1", qty: 10m);
        _parts.AdjustStock(filter.Id, -3m, "Count correction");

        filter.Price = 24.99m;
        filter.QuantityOnHand = 999m; // stale value from an open edit screen
        _parts.Save(filter);

        var saved = _parts.Get(filter.Id)!;
        Assert.Equal(7m, saved.QuantityOnHand);
        Assert.Equal(24.99m, saved.Price);
    }

    [Fact]
    public void Money_and_quantities_round_trip_exactly_through_sqlite()
    {
        var part = _t.AddPart("ODD-1", qty: 0.1m, cost: 0.333m, price: 1234567.89m);

        var saved = _parts.Get(part.Id)!;

        Assert.Equal(0.1m, saved.QuantityOnHand);
        Assert.Equal(0.333m, saved.Cost);
        Assert.Equal(1234567.89m, saved.Price);
    }

    [Fact]
    public void Invoice_from_job_ticket_carries_lines_and_marks_ticket_invoiced_on_finalize()
    {
        var filter = _t.AddPart("OF-1", qty: 5m);
        var tickets = new JobTicketRepository(_t.Db);
        var ticket = new JobTicket
        {
            CustomerId = _t.Customer.Id, VehicleId = _t.Vehicle.Id, Mileage = 84210,
            Complaint = "Oil light on", Lines = { TestDatabase.PartLine(filter, 1m), TestDatabase.Labor("Oil change", 0.5m, 100m) },
        };
        tickets.Save(ticket);

        var inv = _service.CreateFromJobTicket(ticket.Id);
        _service.Finalize(inv.Id);

        var saved = _invoices.Get(inv.Id)!;
        Assert.Equal(2, saved.Lines.Count);
        Assert.Equal(84210, saved.Mileage);
        Assert.Equal(4m, _parts.Get(filter.Id)!.QuantityOnHand);
        Assert.Equal(JobStatus.Invoiced, tickets.Get(ticket.Id)!.Status);
    }
}
