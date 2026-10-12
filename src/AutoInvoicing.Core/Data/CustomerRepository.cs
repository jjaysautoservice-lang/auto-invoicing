using AutoInvoicing.Core.Models;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Core.Data;

public sealed class CustomerRepository(Database db)
{
    public List<Customer> List(string? search = null)
    {
        using var conn = db.Open();
        var like = $"%{search?.Trim() ?? ""}%";
        var customers = conn.Query(
            """
            SELECT DISTINCT c.* FROM customers c
            LEFT JOIN vehicles v ON v.customer_id = c.id
            LEFT JOIN customer_phones p ON p.customer_id = c.id
            WHERE $s = '%%' OR c.name LIKE $s OR p.number LIKE $s OR c.email LIKE $s
               OR v.plate LIKE $s OR v.vin LIKE $s
            ORDER BY c.last_name, c.first_name, c.name
            """, Map, ("$s", like));
        var phones = conn.Query("SELECT * FROM customer_phones ORDER BY customer_id, sort_order", r => (Id: r.Long("customer_id"), Phone: MapPhone(r)))
            .ToLookup(x => x.Id, x => x.Phone);
        foreach (var c in customers)
            c.Phones = phones[c.Id].ToList();
        return customers;
    }

    public Customer? Get(long id)
    {
        using var conn = db.Open();
        var c = conn.Query("SELECT * FROM customers WHERE id = $id", Map, ("$id", id)).FirstOrDefault();
        if (c != null)
            c.Phones = conn.Query("SELECT * FROM customer_phones WHERE customer_id = $id ORDER BY sort_order", MapPhone, ("$id", id));
        return c;
    }

    public void Save(Customer c)
    {
        c.Normalize();
        if (c.LastName.Length == 0)
            throw new ArgumentException("Last name / company is required.");
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        Write(conn, c);
        tx.Commit();
    }

    /// <summary>Inserts or updates an already-normalized customer and replaces their phone numbers.</summary>
    internal static void Write(SqliteConnection conn, Customer c)
    {
        var args = new (string, object?)[]
        {
            ("$id", c.Id), ("$name", c.Name), ("$phone", c.Phone), ("$email", c.Email),
            ("$address", c.Address), ("$notes", c.Notes), ("$last", c.LastName), ("$first", c.FirstName),
            ("$a1", c.Address1), ("$a2", c.Address2), ("$city", c.City), ("$state", c.State), ("$zip", c.Zip),
        };
        if (c.Id == 0)
            c.Id = conn.Insert(
                """
                INSERT INTO customers(name, phone, email, address, notes, last_name, first_name, address1, address2, city, state, zip)
                VALUES ($name, $phone, $email, $address, $notes, $last, $first, $a1, $a2, $city, $state, $zip)
                """, args);
        else
            conn.Exec(
                """
                UPDATE customers SET name=$name, phone=$phone, email=$email, address=$address, notes=$notes,
                    last_name=$last, first_name=$first, address1=$a1, address2=$a2, city=$city, state=$state, zip=$zip
                WHERE id=$id
                """, args);

        conn.Exec("DELETE FROM customer_phones WHERE customer_id=$id", ("$id", c.Id));
        for (var i = 0; i < c.Phones.Count; i++)
        {
            var p = c.Phones[i];
            conn.Exec("INSERT INTO customer_phones(customer_id, sort_order, type, number, note) VALUES ($id, $i, $type, $number, $note)",
                ("$id", c.Id), ("$i", i), ("$type", p.Type), ("$number", p.Number), ("$note", p.Note));
        }
    }

    /// <summary>Deletes a customer and their vehicles. Refuses if they have any tickets or invoices.</summary>
    public void Delete(long id)
    {
        using var conn = db.Open();
        var used = (long)conn.Scalar(
            "SELECT (SELECT COUNT(*) FROM invoices WHERE customer_id=$id) + (SELECT COUNT(*) FROM job_tickets WHERE customer_id=$id)",
            ("$id", id))!;
        if (used > 0)
            throw new InvalidOperationException("This customer has job tickets or invoices and cannot be deleted.");
        conn.Exec("DELETE FROM vehicles WHERE customer_id=$id", ("$id", id));
        conn.Exec("DELETE FROM customer_phones WHERE customer_id=$id", ("$id", id));
        conn.Exec("DELETE FROM customers WHERE id=$id", ("$id", id));
    }

    internal static Customer Map(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        Name = r.Str("name"),
        Phone = r.Str("phone"),
        Email = r.Str("email"),
        Address = r.Str("address"),
        Notes = r.Str("notes"),
        LastName = r.Str("last_name"),
        FirstName = r.Str("first_name"),
        Address1 = r.Str("address1"),
        Address2 = r.Str("address2"),
        City = r.Str("city"),
        State = r.Str("state"),
        Zip = r.Str("zip"),
    };

    private static CustomerPhone MapPhone(SqliteDataReader r) => new()
    {
        Type = r.Str("type"),
        Number = r.Str("number"),
        Note = r.Str("note"),
    };
}
