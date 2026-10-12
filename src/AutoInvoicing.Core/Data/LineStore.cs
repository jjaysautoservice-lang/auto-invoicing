using AutoInvoicing.Core.Models;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Core.Data;

/// <summary>Shared read/write for invoice_lines and job_ticket_lines, which have the same shape.</summary>
internal static class LineStore
{
    public static List<LineItem> Load(SqliteConnection conn, string table, string fk, long ownerId) =>
        conn.Query($"SELECT * FROM {table} WHERE {fk}=$id ORDER BY sort_order, id", r => new LineItem
        {
            Id = r.Long("id"),
            Type = (LineType)r.Int("type"),
            PartId = r.NLong("part_id"),
            PartNumber = r.Str("part_number"),
            Description = r.Str("description"),
            Quantity = r.Decimal("quantity"),
            UnitPrice = r.Decimal("unit_price"),
            UnitCost = r.Decimal("unit_cost"),
            Taxable = r.Bool("taxable"),
        }, ("$id", ownerId));

    public static void Replace(SqliteConnection conn, string table, string fk, long ownerId, IList<LineItem> lines)
    {
        conn.Exec($"DELETE FROM {table} WHERE {fk}=$id", ("$id", ownerId));
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            l.Id = conn.Insert(
                $"""
                INSERT INTO {table}({fk}, sort_order, type, part_id, part_number, description, quantity, unit_price, unit_cost, taxable)
                VALUES ($o, $sort, $type, $part, $num, $desc, $qty, $price, $cost, $taxable)
                """,
                ("$o", ownerId), ("$sort", i), ("$type", l.Type), ("$part", l.PartId), ("$num", l.PartNumber),
                ("$desc", l.Description), ("$qty", l.Quantity), ("$price", l.UnitPrice), ("$cost", l.UnitCost),
                ("$taxable", l.Taxable));
        }
    }

    public static int NextNumber(SqliteConnection conn, string counter) =>
        Convert.ToInt32(conn.Scalar(
            "UPDATE counters SET next_value = next_value + 1 WHERE name=$n RETURNING next_value - 1", ("$n", counter)));
}
