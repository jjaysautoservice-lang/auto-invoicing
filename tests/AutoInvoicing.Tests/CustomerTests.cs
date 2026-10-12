using AutoInvoicing.Core.Data;
using AutoInvoicing.Core.Models;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Tests;

public class CustomerTests : IDisposable
{
    private readonly TestDatabase _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public void Name_phone_and_address_are_built_from_the_separate_fields()
    {
        var c = new Customer
        {
            LastName = " Smith ", FirstName = "Jane", Address1 = "12 Main St", Address2 = "Apt 4",
            City = "Springfield", State = "il", Zip = "62701",
            Phones =
            {
                new CustomerPhone { Type = "Home", Number = "" },
                new CustomerPhone { Type = "Cell", Number = " 555-0101 ", Note = "texts ok" },
                new CustomerPhone { Type = "Work", Number = "555-0102" },
            },
        };

        c.Normalize();

        Assert.Equal("Jane Smith", c.Name);
        Assert.Equal("555-0101", c.Phone);
        Assert.Equal(2, c.Phones.Count);
        Assert.Equal("IL", c.State);
        Assert.Equal(string.Join(Environment.NewLine, "12 Main St", "Apt 4", "Springfield, IL 62701"), c.Address);
    }

    [Fact]
    public void A_company_with_no_first_name_uses_the_company_name()
    {
        var c = new Customer { LastName = "Acme Towing" };
        c.Normalize();
        Assert.Equal("Acme Towing", c.Name);
    }

    [Fact]
    public void Last_name_or_company_is_required()
    {
        var ex = Assert.Throws<ArgumentException>(() => new CustomerRepository(_t.Db).Save(new Customer { FirstName = "Jane" }));
        Assert.Contains("Last name", ex.Message);
    }

    [Fact]
    public void Phones_round_trip_in_order_and_are_searchable()
    {
        var repo = new CustomerRepository(_t.Db);
        var c = new Customer
        {
            LastName = "Smith", FirstName = "Jane",
            Phones =
            {
                new CustomerPhone { Type = "Cell", Number = "555-0101", Note = "texts ok" },
                new CustomerPhone { Type = "Work", Number = "555-0199", Note = "ask for Jane" },
            },
        };
        repo.Save(c);

        var loaded = repo.Get(c.Id)!;
        Assert.Equal(new[] { "555-0101", "555-0199" }, loaded.Phones.Select(p => p.Number));
        Assert.Equal("Work", loaded.Phones[1].Type);
        Assert.Equal("ask for Jane", loaded.Phones[1].Note);
        Assert.Equal("555-0101", loaded.Phone);

        Assert.Equal(c.Id, Assert.Single(repo.List("0199")).Id);
        Assert.Equal(2, repo.List().Single(x => x.Id == c.Id).Phones.Count);

        loaded.Phones.RemoveAt(0);
        repo.Save(loaded);
        Assert.Equal("555-0199", Assert.Single(repo.Get(c.Id)!.Phones).Number);
    }

    [Fact]
    public void Customers_from_an_older_database_keep_their_name_phone_and_address()
    {
        var path = Path.Combine(Path.GetTempPath(), $"autoinv-legacy-{Guid.NewGuid():N}.db");
        try
        {
            using (var conn = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    """
                    CREATE TABLE customers (
                        id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, phone TEXT NOT NULL DEFAULT '',
                        email TEXT NOT NULL DEFAULT '', address TEXT NOT NULL DEFAULT '', notes TEXT NOT NULL DEFAULT '');
                    INSERT INTO customers(name, phone, email, address) VALUES ('Old Timer', '555-0123', 'old@example.com', '9 Elm Rd' || char(13) || char(10) || 'Shelbyville');
                    """;
                cmd.ExecuteNonQuery();
            }

            var db = new Database(path);
            db.Initialize();
            db.Initialize(); // upgrading twice must be harmless

            var c = Assert.Single(new CustomerRepository(db).List());
            Assert.Equal("Old Timer", c.Name);
            Assert.Equal("Old Timer", c.LastName);
            Assert.Equal("555-0123", Assert.Single(c.Phones).Number);
            Assert.Equal("9 Elm Rd", c.Address1);
            Assert.Equal("Shelbyville", c.Address2);
            Assert.Equal("old@example.com", c.Email);
        }
        finally
        {
            foreach (var f in new[] { path, path + "-wal", path + "-shm" })
                try { File.Delete(f); } catch (IOException) { }
        }
    }
}
