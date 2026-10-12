using AutoInvoicing.Core.Models;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Core.Data;

public sealed class VehicleRepository(Database db)
{
    public List<Vehicle> ListByCustomer(long customerId)
    {
        using var conn = db.Open();
        return conn.Query("SELECT * FROM vehicles WHERE customer_id=$c ORDER BY year DESC, make, model", Map, ("$c", customerId));
    }

    public Vehicle? Get(long id)
    {
        using var conn = db.Open();
        return conn.Query("SELECT * FROM vehicles WHERE id=$id", Map, ("$id", id)).FirstOrDefault();
    }

    public void Save(Vehicle v)
    {
        if (v.CustomerId == 0)
            throw new ArgumentException("Vehicle must belong to a customer.");
        using var conn = db.Open();
        var args = new (string, object?)[]
        {
            ("$id", v.Id), ("$c", v.CustomerId), ("$year", v.Year), ("$make", v.Make), ("$model", v.Model),
            ("$vin", v.Vin.ToUpperInvariant()), ("$plate", v.Plate.ToUpperInvariant()), ("$color", v.Color), ("$engine", v.Engine),
        };
        if (v.Id == 0)
            v.Id = conn.Insert(
                "INSERT INTO vehicles(customer_id, year, make, model, vin, plate, color, engine) VALUES ($c, $year, $make, $model, $vin, $plate, $color, $engine)",
                args);
        else
            conn.Exec(
                "UPDATE vehicles SET customer_id=$c, year=$year, make=$make, model=$model, vin=$vin, plate=$plate, color=$color, engine=$engine WHERE id=$id",
                args);
    }

    public void Delete(long id)
    {
        using var conn = db.Open();
        var used = (long)conn.Scalar(
            "SELECT (SELECT COUNT(*) FROM invoices WHERE vehicle_id=$id) + (SELECT COUNT(*) FROM job_tickets WHERE vehicle_id=$id)",
            ("$id", id))!;
        if (used > 0)
            throw new InvalidOperationException("This vehicle has job tickets or invoices and cannot be deleted.");
        conn.Exec("DELETE FROM vehicles WHERE id=$id", ("$id", id));
    }

    private static Vehicle Map(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        CustomerId = r.Long("customer_id"),
        Year = r.NInt("year"),
        Make = r.Str("make"),
        Model = r.Str("model"),
        Vin = r.Str("vin"),
        Plate = r.Str("plate"),
        Color = r.Str("color"),
        Engine = r.Str("engine"),
    };
}
