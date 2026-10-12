using AutoInvoicing.Core.Data;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.Tests;

/// <summary>A throwaway SQLite file per test, with one customer and vehicle already created.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"autoinv-test-{Guid.NewGuid():N}.db");

    public Database Db { get; }
    public Customer Customer { get; } = new() { Name = "Pat Driver", Phone = "555-0100" };
    public Vehicle Vehicle { get; } = new() { Year = 2018, Make = "Honda", Model = "Civic", Plate = "ABC123" };

    public TestDatabase()
    {
        Db = new Database(_path);
        Db.Initialize();
        new CustomerRepository(Db).Save(Customer);
        Vehicle.CustomerId = Customer.Id;
        new VehicleRepository(Db).Save(Vehicle);
    }

    public Part AddPart(string number, decimal qty, decimal cost = 10m, decimal price = 20m, decimal reorder = 0m)
    {
        var part = new Part { PartNumber = number, Description = "Part " + number, Cost = cost, Price = price, QuantityOnHand = qty, ReorderLevel = reorder };
        new PartRepository(Db).Save(part);
        return part;
    }

    public static LineItem PartLine(Part p, decimal qty) => new()
    {
        Type = LineType.Part, PartId = p.Id, PartNumber = p.PartNumber, Description = p.Description,
        Quantity = qty, UnitPrice = p.Price, UnitCost = p.Cost, Taxable = true,
    };

    public static LineItem Labor(string description, decimal hours, decimal rate) => new()
    {
        Type = LineType.Labor, Description = description, Quantity = hours, UnitPrice = rate, Taxable = false,
    };

    public void Dispose()
    {
        try { File.Delete(_path); } catch (IOException) { }
        try { File.Delete(_path + "-wal"); } catch (IOException) { }
        try { File.Delete(_path + "-shm"); } catch (IOException) { }
    }
}
