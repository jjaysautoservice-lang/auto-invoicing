using AutoInvoicing.Core.Data;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.Core.Services;

public sealed record StockWarning(long PartId, string PartNumber, string Description, decimal QuantityOnHand, decimal ReorderLevel)
{
    public bool IsNegative => QuantityOnHand < 0m;

    public override string ToString() => IsNegative
        ? $"{PartNumber} {Description}: on hand is now {QuantityOnHand} (below zero, recount this part)"
        : $"{PartNumber} {Description}: {QuantityOnHand} left (reorder at {ReorderLevel})";
}

/// <summary>
/// Invoice lifecycle: draft, finalize (deducts parts from inventory), void (puts them back), payments.
/// </summary>
public sealed class InvoiceService(Database db)
{
    private readonly InvoiceRepository _invoices = new(db);
    private readonly JobTicketRepository _tickets = new(db);
    private readonly SettingsRepository _settings = new(db);

    public Invoice NewDraft(long customerId, long vehicleId)
    {
        var settings = _settings.Load();
        return new Invoice
        {
            CustomerId = customerId,
            VehicleId = vehicleId,
            InvoiceDate = DateTime.Today,
            TaxRatePercent = settings.TaxRatePercent,
        };
    }

    /// <summary>Creates and saves a draft invoice carrying over the ticket's lines, mileage and work notes.</summary>
    public Invoice CreateFromJobTicket(long jobTicketId)
    {
        var ticket = _tickets.Get(jobTicketId) ?? throw new InvalidOperationException("Job ticket not found.");
        var inv = NewDraft(ticket.CustomerId, ticket.VehicleId);
        inv.JobTicketId = ticket.Id;
        inv.Mileage = ticket.Mileage;
        inv.Lines = ticket.Lines.Select(l => { var c = l.Clone(); c.Id = 0; return c; }).ToList();
        inv.Notes = string.Join(Environment.NewLine, new[]
        {
            ticket.Complaint.Length > 0 ? "Concern: " + ticket.Complaint : "",
            ticket.Cause.Length > 0 ? "Cause: " + ticket.Cause : "",
            ticket.Correction.Length > 0 ? "Correction: " + ticket.Correction : "",
        }.Where(s => s.Length > 0));
        _invoices.SaveDraft(inv);
        return inv;
    }

    /// <summary>
    /// Locks the invoice and deducts every inventory part on it from stock, in one transaction.
    /// Stock is allowed to go below zero (the part was physically on the car), and those parts
    /// come back as warnings along with anything at or under its reorder level.
    /// </summary>
    public IReadOnlyList<StockWarning> Finalize(long invoiceId)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        var inv = InvoiceRepository.Get(conn, invoiceId) ?? throw new InvalidOperationException("Invoice not found.");
        if (inv.Status != InvoiceStatus.Draft)
            throw new InvalidOperationException($"Invoice #{inv.Number} is already {inv.Status}.");
        if (inv.Lines.Count == 0)
            throw new InvalidOperationException("Add at least one line before finalizing.");

        var warnings = new List<StockWarning>();
        foreach (var group in InventoryLines(inv))
        {
            PartRepository.ApplyStockChange(conn, group.PartId, -group.Quantity, $"Sold on invoice #{inv.Number}", inv.Id);
            var part = PartRepository.Get(conn, group.PartId)!;
            if (part.QuantityOnHand < 0m || part.IsLowStock)
                warnings.Add(new StockWarning(part.Id, part.PartNumber, part.Description, part.QuantityOnHand, part.ReorderLevel));
        }

        conn.Exec("UPDATE invoices SET status=$s WHERE id=$id", ("$s", InvoiceStatus.Finalized), ("$id", inv.Id));
        if (inv.JobTicketId is long ticketId)
            JobTicketRepository.SetStatus(conn, ticketId, JobStatus.Invoiced);
        tx.Commit();
        return warnings;
    }

    /// <summary>Voids an invoice. If it was finalized, its parts go back into stock.</summary>
    public void Void(long invoiceId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to void an invoice.");
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        var inv = InvoiceRepository.Get(conn, invoiceId) ?? throw new InvalidOperationException("Invoice not found.");
        if (inv.Status == InvoiceStatus.Void)
            throw new InvalidOperationException($"Invoice #{inv.Number} is already void.");

        if (inv.Status == InvoiceStatus.Finalized)
        {
            foreach (var group in InventoryLines(inv))
                PartRepository.ApplyStockChange(conn, group.PartId, group.Quantity, $"Returned, invoice #{inv.Number} voided", inv.Id);
        }

        conn.Exec("UPDATE invoices SET status=$s, void_reason=$r WHERE id=$id",
            ("$s", InvoiceStatus.Void), ("$r", reason.Trim()), ("$id", inv.Id));
        if (inv.JobTicketId is long ticketId)
            JobTicketRepository.SetStatus(conn, ticketId, JobStatus.Completed);
        tx.Commit();
    }

    public Payment AddPayment(long invoiceId, decimal amount, string method, DateTime date, string reference = "")
    {
        if (amount == 0m)
            throw new ArgumentException("Payment amount cannot be zero.");
        if (Money.Round(amount) != amount)
            throw new ArgumentException("Payment amount must be in whole cents.");
        using var conn = db.Open();
        var inv = InvoiceRepository.Get(conn, invoiceId) ?? throw new InvalidOperationException("Invoice not found.");
        if (inv.Status == InvoiceStatus.Void)
            throw new InvalidOperationException("Cannot take a payment on a void invoice.");
        var payment = new Payment { InvoiceId = invoiceId, Amount = amount, Method = method, Date = date.Date, Reference = reference };
        payment.Id = conn.Insert("INSERT INTO payments(invoice_id, date, amount, method, reference) VALUES ($i, $d, $a, $m, $r)",
            ("$i", invoiceId), ("$d", payment.Date), ("$a", amount), ("$m", method), ("$r", reference));
        return payment;
    }

    public void DeletePayment(long paymentId)
    {
        using var conn = db.Open();
        conn.Exec("DELETE FROM payments WHERE id=$id", ("$id", paymentId));
    }

    private static IEnumerable<(long PartId, decimal Quantity)> InventoryLines(Invoice inv) =>
        inv.Lines
            .Where(l => l.Type == LineType.Part && l.PartId.HasValue && l.Quantity != 0m)
            .GroupBy(l => l.PartId!.Value)
            .Select(g => (g.Key, g.Sum(l => l.Quantity)));
}
