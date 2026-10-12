using AutoInvoicing.Core.Models;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Core.Data;

public sealed class InvoiceRepository(Database db)
{
    public List<InvoiceSummary> List(DateTime? from = null, DateTime? to = null, string? search = null)
    {
        var like = $"%{search?.Trim() ?? ""}%";
        return Summaries(
            """
            WHERE ($from IS NULL OR i.invoice_date >= $from) AND ($to IS NULL OR i.invoice_date <= $to)
              AND ($s = '%%' OR c.name LIKE $s OR CAST(i.number AS TEXT) LIKE $s OR v.plate LIKE $s)
            ORDER BY i.number DESC
            """, ("$from", from?.Date), ("$to", to?.Date), ("$s", like));
    }

    public List<InvoiceSummary> ListByCustomer(long customerId) =>
        Summaries("WHERE i.customer_id=$c ORDER BY i.invoice_date DESC, i.number DESC", ("$c", customerId));

    public List<InvoiceSummary> ListByVehicle(long vehicleId) =>
        Summaries("WHERE i.vehicle_id=$v ORDER BY i.invoice_date DESC, i.number DESC", ("$v", vehicleId));

    public Invoice? Get(long id)
    {
        using var conn = db.Open();
        return Get(conn, id);
    }

    internal static Invoice? Get(SqliteConnection conn, long id)
    {
        var inv = conn.Query("SELECT * FROM invoices WHERE id=$id", Map, ("$id", id)).FirstOrDefault();
        if (inv == null)
            return null;
        inv.Lines = LineStore.Load(conn, "invoice_lines", "invoice_id", id);
        inv.Payments = LoadPayments(conn, "WHERE invoice_id=$id ORDER BY date, id", ("$id", id));
        return inv;
    }

    /// <summary>Loads full invoices (lines and payments) that are finalized and dated within [from, to].</summary>
    public List<Invoice> FinalizedBetween(DateTime from, DateTime to)
    {
        using var conn = db.Open();
        var ids = conn.Query("SELECT id FROM invoices WHERE status=$s AND invoice_date >= $from AND invoice_date <= $to ORDER BY number",
            r => r.GetInt64(0), ("$s", InvoiceStatus.Finalized), ("$from", from.Date), ("$to", to.Date));
        return ids.Select(id => Get(conn, id)!).ToList();
    }

    /// <summary>Payments received within [from, to] on invoices that are not void.</summary>
    public List<Payment> PaymentsBetween(DateTime from, DateTime to)
    {
        using var conn = db.Open();
        return LoadPayments(conn,
            """
            WHERE date >= $from AND date <= $to
              AND invoice_id IN (SELECT id FROM invoices WHERE status <> $void)
            ORDER BY date, id
            """, ("$from", from.Date), ("$to", to.Date), ("$void", InvoiceStatus.Void));
    }

    /// <summary>Saves header and lines. Only draft invoices can be edited.</summary>
    public void SaveDraft(Invoice inv)
    {
        if (inv.CustomerId == 0 || inv.VehicleId == 0)
            throw new ArgumentException("An invoice needs a customer and a vehicle.");
        if (inv.TaxRatePercent < 0m)
            throw new ArgumentException("Tax rate cannot be negative.");
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        if (inv.Id == 0)
        {
            inv.Number = LineStore.NextNumber(conn, "invoice");
        }
        else
        {
            var status = (InvoiceStatus)Convert.ToInt32(conn.Scalar("SELECT status FROM invoices WHERE id=$id", ("$id", inv.Id)));
            if (status != InvoiceStatus.Draft)
                throw new InvalidOperationException($"Invoice #{inv.Number} is {status} and can no longer be edited.");
        }
        var args = new (string, object?)[]
        {
            ("$id", inv.Id), ("$num", inv.Number), ("$c", inv.CustomerId), ("$v", inv.VehicleId), ("$t", inv.JobTicketId),
            ("$date", inv.InvoiceDate.Date), ("$miles", inv.Mileage), ("$rate", inv.TaxRatePercent), ("$notes", inv.Notes),
        };
        if (inv.Id == 0)
            inv.Id = conn.Insert(
                """
                INSERT INTO invoices(number, customer_id, vehicle_id, job_ticket_id, invoice_date, status, mileage, tax_rate_percent, notes)
                VALUES ($num, $c, $v, $t, $date, 0, $miles, $rate, $notes)
                """, args);
        else
            conn.Exec(
                """
                UPDATE invoices SET customer_id=$c, vehicle_id=$v, job_ticket_id=$t, invoice_date=$date, mileage=$miles,
                    tax_rate_percent=$rate, notes=$notes
                WHERE id=$id
                """, args);
        LineStore.Replace(conn, "invoice_lines", "invoice_id", inv.Id, inv.Lines);
        tx.Commit();
    }

    public void DeleteDraft(long id)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        var status = conn.Scalar("SELECT status FROM invoices WHERE id=$id", ("$id", id));
        if (status == null)
            return;
        if ((InvoiceStatus)Convert.ToInt32(status) != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be deleted. Void a finalized invoice instead.");
        conn.Exec("DELETE FROM invoice_lines WHERE invoice_id=$id", ("$id", id));
        conn.Exec("DELETE FROM payments WHERE invoice_id=$id", ("$id", id));
        conn.Exec("DELETE FROM invoices WHERE id=$id", ("$id", id));
        tx.Commit();
    }

    private List<InvoiceSummary> Summaries(string where, params (string, object?)[] args)
    {
        using var conn = db.Open();
        var headers = conn.Query(
            $"""
            SELECT i.id, i.number, i.invoice_date, i.status, i.customer_id, c.name AS customer_name,
                   i.vehicle_id, v.year, v.make, v.model, i.mileage
            FROM invoices i
            JOIN customers c ON c.id = i.customer_id
            JOIN vehicles v ON v.id = i.vehicle_id
            {where}
            """,
            r => new InvoiceSummary
            {
                Id = r.Long("id"),
                Number = r.Int("number"),
                InvoiceDate = r.DateTime("invoice_date"),
                Status = (InvoiceStatus)r.Int("status"),
                CustomerId = r.Long("customer_id"),
                CustomerName = r.Str("customer_name"),
                VehicleId = r.Long("vehicle_id"),
                VehicleDescription = new Vehicle { Year = r.NInt("year"), Make = r.Str("make"), Model = r.Str("model") }.Description,
                Mileage = r.NInt("mileage"),
            }, args);

        // Totals are computed in C# with decimals rather than SQL SUM, which would use floating point.
        foreach (var h in headers)
        {
            var inv = Get(conn, h.Id)!;
            var totals = inv.Totals;
            h.Total = totals.Total;
            h.BalanceDue = h.Status == InvoiceStatus.Void ? 0m : totals.BalanceDue;
            h.WorkSummary = string.Join("; ", inv.Lines.Where(l => l.Type != LineType.Part).Select(l => l.Description));
        }
        return headers;
    }

    private static List<Payment> LoadPayments(SqliteConnection conn, string where, params (string, object?)[] args) =>
        conn.Query($"SELECT * FROM payments {where}", r => new Payment
        {
            Id = r.Long("id"),
            InvoiceId = r.Long("invoice_id"),
            Date = r.DateTime("date"),
            Amount = r.Decimal("amount"),
            Method = r.Str("method"),
            Reference = r.Str("reference"),
        }, args);

    private static Invoice Map(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        Number = r.Int("number"),
        CustomerId = r.Long("customer_id"),
        VehicleId = r.Long("vehicle_id"),
        JobTicketId = r.NLong("job_ticket_id"),
        InvoiceDate = r.DateTime("invoice_date"),
        Status = (InvoiceStatus)r.Int("status"),
        Mileage = r.NInt("mileage"),
        TaxRatePercent = r.Decimal("tax_rate_percent"),
        Notes = r.Str("notes"),
        VoidReason = r.Str("void_reason"),
    };
}
