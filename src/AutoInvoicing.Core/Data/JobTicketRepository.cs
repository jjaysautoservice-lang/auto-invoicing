using AutoInvoicing.Core.Models;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Core.Data;

public sealed class JobTicketRepository(Database db)
{
    private const string SummarySql = """
        SELECT t.id, t.number, t.date_opened, t.status, t.customer_id, c.name AS customer_name,
               t.vehicle_id, v.year, v.make, v.model, t.technician, t.complaint
        FROM job_tickets t
        JOIN customers c ON c.id = t.customer_id
        JOIN vehicles v ON v.id = t.vehicle_id
        """;

    public List<JobTicketSummary> List(bool openOnly = false, string? search = null)
    {
        using var conn = db.Open();
        var like = $"%{search?.Trim() ?? ""}%";
        return conn.Query(SummarySql + """

            WHERE ($open = 0 OR t.status IN (0, 1, 2))
              AND ($s = '%%' OR c.name LIKE $s OR CAST(t.number AS TEXT) LIKE $s OR v.plate LIKE $s OR t.complaint LIKE $s)
            ORDER BY t.number DESC
            """, MapSummary, ("$open", openOnly), ("$s", like));
    }

    public List<JobTicketSummary> ListByCustomer(long customerId)
    {
        using var conn = db.Open();
        return conn.Query(SummarySql + " WHERE t.customer_id=$c ORDER BY t.date_opened DESC, t.number DESC", MapSummary, ("$c", customerId));
    }

    public List<JobTicketSummary> ListByVehicle(long vehicleId)
    {
        using var conn = db.Open();
        return conn.Query(SummarySql + " WHERE t.vehicle_id=$v ORDER BY t.date_opened DESC, t.number DESC", MapSummary, ("$v", vehicleId));
    }

    public JobTicket? Get(long id)
    {
        using var conn = db.Open();
        var t = conn.Query("SELECT * FROM job_tickets WHERE id=$id", Map, ("$id", id)).FirstOrDefault();
        if (t != null)
            t.Lines = LineStore.Load(conn, "job_ticket_lines", "job_ticket_id", id);
        return t;
    }

    public void Save(JobTicket t)
    {
        if (t.CustomerId == 0 || t.VehicleId == 0)
            throw new ArgumentException("A job ticket needs a customer and a vehicle.");
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        if (t.Id == 0)
            t.Number = LineStore.NextNumber(conn, "ticket");
        var args = new (string, object?)[]
        {
            ("$id", t.Id), ("$num", t.Number), ("$c", t.CustomerId), ("$v", t.VehicleId), ("$opened", t.DateOpened.Date),
            ("$promised", t.Promised), ("$status", t.Status), ("$miles", t.Mileage), ("$tech", t.Technician),
            ("$complaint", t.Complaint), ("$cause", t.Cause), ("$correction", t.Correction), ("$notes", t.Notes),
        };
        if (t.Id == 0)
            t.Id = conn.Insert(
                """
                INSERT INTO job_tickets(number, customer_id, vehicle_id, date_opened, promised, status, mileage, technician, complaint, cause, correction, notes)
                VALUES ($num, $c, $v, $opened, $promised, $status, $miles, $tech, $complaint, $cause, $correction, $notes)
                """, args);
        else
            conn.Exec(
                """
                UPDATE job_tickets SET customer_id=$c, vehicle_id=$v, date_opened=$opened, promised=$promised, status=$status,
                    mileage=$miles, technician=$tech, complaint=$complaint, cause=$cause, correction=$correction, notes=$notes
                WHERE id=$id
                """, args);
        LineStore.Replace(conn, "job_ticket_lines", "job_ticket_id", t.Id, t.Lines);
        tx.Commit();
    }

    internal static void SetStatus(SqliteConnection conn, long id, JobStatus status) =>
        conn.Exec("UPDATE job_tickets SET status=$s WHERE id=$id", ("$s", status), ("$id", id));

    private static JobTicket Map(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        Number = r.Int("number"),
        CustomerId = r.Long("customer_id"),
        VehicleId = r.Long("vehicle_id"),
        DateOpened = r.DateTime("date_opened"),
        Promised = r.NDateTime("promised"),
        Status = (JobStatus)r.Int("status"),
        Mileage = r.NInt("mileage"),
        Technician = r.Str("technician"),
        Complaint = r.Str("complaint"),
        Cause = r.Str("cause"),
        Correction = r.Str("correction"),
        Notes = r.Str("notes"),
    };

    private static JobTicketSummary MapSummary(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        Number = r.Int("number"),
        DateOpened = r.DateTime("date_opened"),
        Status = (JobStatus)r.Int("status"),
        CustomerId = r.Long("customer_id"),
        CustomerName = r.Str("customer_name"),
        VehicleId = r.Long("vehicle_id"),
        VehicleDescription = new Vehicle { Year = r.NInt("year"), Make = r.Str("make"), Model = r.Str("model") }.Description,
        Technician = r.Str("technician"),
        Complaint = r.Str("complaint"),
    };
}
