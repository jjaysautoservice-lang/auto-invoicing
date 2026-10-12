using AutoInvoicing.Core.Models;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Core.Data;

public sealed class CustomerRepository(Database db)
{
    public List<Customer> List(string? search = null)
    {
        using var conn = db.Open();
        var like = $"%{search?.Trim() ?? ""}%";
        return conn.Query(
            """
            SELECT DISTINCT c.* FROM customers c
            LEFT JOIN vehicles v ON v.customer_id = c.id
            WHERE $s = '%%' OR c.name LIKE $s OR c.phone LIKE $s OR c.email LIKE $s
               OR v.plate LIKE $s OR v.vin LIKE $s
            ORDER BY c.name
            """, Map, ("$s", like));
    }

    public Customer? Get(long id)
    {
        using var conn = db.Open();
        return conn.Query("SELECT * FROM customers WHERE id = $id", Map, ("$id", id)).FirstOrDefault();
    }

    public void Save(Customer c)
    {
        if (string.IsNullOrWhiteSpace(c.Name))
            throw new ArgumentException("Customer name is required.");
        using var conn = db.Open();
        var args = new (string, object?)[]
        {
            ("$id", c.Id), ("$name", c.Name.Trim()), ("$phone", c.Phone), ("$email", c.Email),
            ("$address", c.Address), ("$notes", c.Notes),
        };
        if (c.Id == 0)
            c.Id = conn.Insert("INSERT INTO customers(name, phone, email, address, notes) VALUES ($name, $phone, $email, $address, $notes)", args);
        else
            conn.Exec("UPDATE customers SET name=$name, phone=$phone, email=$email, address=$address, notes=$notes WHERE id=$id", args);
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
        conn.Exec("DELETE FROM customers WHERE id=$id", ("$id", id));
    }

    private static Customer Map(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        Name = r.Str("name"),
        Phone = r.Str("phone"),
        Email = r.Str("email"),
        Address = r.Str("address"),
        Notes = r.Str("notes"),
    };
}
