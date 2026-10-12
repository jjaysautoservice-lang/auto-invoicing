using AutoInvoicing.Core.Models;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Core.Data;

public sealed class PartRepository(Database db)
{
    public List<Part> List(string? search = null, bool includeInactive = false, bool lowStockOnly = false)
    {
        using var conn = db.Open();
        var like = $"%{search?.Trim() ?? ""}%";
        var parts = conn.Query(
            """
            SELECT * FROM parts
            WHERE ($s = '%%' OR part_number LIKE $s OR description LIKE $s OR vendor LIKE $s)
              AND ($inactive = 1 OR active = 1)
            ORDER BY part_number
            """, Map, ("$s", like), ("$inactive", includeInactive));
        return lowStockOnly ? parts.Where(p => p.IsLowStock).ToList() : parts;
    }

    public Part? Get(long id)
    {
        using var conn = db.Open();
        return Get(conn, id);
    }

    internal static Part? Get(SqliteConnection conn, long id) =>
        conn.Query("SELECT * FROM parts WHERE id=$id", Map, ("$id", id)).FirstOrDefault();

    public Part? GetByNumber(string partNumber)
    {
        using var conn = db.Open();
        return conn.Query("SELECT * FROM parts WHERE part_number=$n COLLATE NOCASE", Map, ("$n", partNumber.Trim())).FirstOrDefault();
    }

    /// <summary>
    /// Saves part details. Quantity on hand is only set when the part is first created;
    /// after that use <see cref="AdjustStock"/> so every change is logged.
    /// </summary>
    public void Save(Part p)
    {
        if (string.IsNullOrWhiteSpace(p.PartNumber))
            throw new ArgumentException("Part number is required.");
        if (p.Cost < 0m || p.Price < 0m)
            throw new ArgumentException("Cost and price cannot be negative.");
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        var args = new (string, object?)[]
        {
            ("$id", p.Id), ("$num", p.PartNumber.Trim()), ("$desc", p.Description), ("$vendor", p.Vendor),
            ("$loc", p.Location), ("$cost", p.Cost), ("$price", p.Price), ("$qty", p.QuantityOnHand),
            ("$reorder", p.ReorderLevel), ("$taxable", p.Taxable), ("$active", p.Active),
        };
        if (p.Id == 0)
        {
            p.Id = conn.Insert(
                """
                INSERT INTO parts(part_number, description, vendor, location, cost, price, quantity_on_hand, reorder_level, taxable, active)
                VALUES ($num, $desc, $vendor, $loc, $cost, $price, $qty, $reorder, $taxable, $active)
                """, args);
            if (p.QuantityOnHand != 0m)
                LogTransaction(conn, p.Id, p.QuantityOnHand, "Opening stock", null);
        }
        else
        {
            conn.Exec(
                """
                UPDATE parts SET part_number=$num, description=$desc, vendor=$vendor, location=$loc, cost=$cost,
                    price=$price, reorder_level=$reorder, taxable=$taxable, active=$active
                WHERE id=$id
                """, args);
            p.QuantityOnHand = Get(conn, p.Id)!.QuantityOnHand;
        }
        tx.Commit();
    }

    /// <summary>Receives stock (positive) or corrects a count (either sign) and logs the change.</summary>
    public decimal AdjustStock(long partId, decimal quantityChange, string reason)
    {
        if (quantityChange == 0m)
            throw new ArgumentException("Quantity change cannot be zero.");
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        var newQty = ApplyStockChange(conn, partId, quantityChange, reason, null);
        tx.Commit();
        return newQty;
    }

    public List<InventoryTransaction> Transactions(long partId)
    {
        using var conn = db.Open();
        return conn.Query("SELECT * FROM inventory_transactions WHERE part_id=$p ORDER BY date DESC, id DESC",
            r => new InventoryTransaction
            {
                Id = r.Long("id"),
                PartId = r.Long("part_id"),
                Date = r.DateTime("date"),
                QuantityChange = r.Decimal("quantity_change"),
                Reason = r.Str("reason"),
                InvoiceId = r.NLong("invoice_id"),
            }, ("$p", partId));
    }

    /// <summary>Changes on-hand quantity and writes the audit row. Caller owns the transaction.</summary>
    internal static decimal ApplyStockChange(SqliteConnection conn, long partId, decimal change, string reason, long? invoiceId)
    {
        var part = Get(conn, partId) ?? throw new InvalidOperationException($"Part {partId} no longer exists.");
        var newQty = part.QuantityOnHand + change;
        conn.Exec("UPDATE parts SET quantity_on_hand=$q WHERE id=$id", ("$q", newQty), ("$id", partId));
        LogTransaction(conn, partId, change, reason, invoiceId);
        return newQty;
    }

    private static void LogTransaction(SqliteConnection conn, long partId, decimal change, string reason, long? invoiceId) =>
        conn.Exec(
            "INSERT INTO inventory_transactions(part_id, date, quantity_change, reason, invoice_id) VALUES ($p, $d, $q, $r, $i)",
            ("$p", partId), ("$d", DateTime.Now), ("$q", change), ("$r", reason), ("$i", invoiceId));

    internal static Part Map(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        PartNumber = r.Str("part_number"),
        Description = r.Str("description"),
        Vendor = r.Str("vendor"),
        Location = r.Str("location"),
        Cost = r.Decimal("cost"),
        Price = r.Decimal("price"),
        QuantityOnHand = r.Decimal("quantity_on_hand"),
        ReorderLevel = r.Decimal("reorder_level"),
        Taxable = r.Bool("taxable"),
        Active = r.Bool("active"),
    };
}
