using AutoInvoicing.Core.Data;
using AutoInvoicing.Core.Models;
using AutoInvoicing.Core.Services;

namespace AutoInvoicing.Tests;

public class HistoryTests : IDisposable
{
    private readonly TestDatabase _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public void History_lists_invoices_per_customer_and_per_vehicle()
    {
        var vehicles = new VehicleRepository(_t.Db);
        var truck = new Vehicle { CustomerId = _t.Customer.Id, Year = 2015, Make = "Ford", Model = "F-150" };
        vehicles.Save(truck);
        var service = new InvoiceService(_t.Db);
        var invoices = new InvoiceRepository(_t.Db);

        foreach (var vehicleId in new[] { _t.Vehicle.Id, truck.Id, truck.Id })
        {
            var inv = service.NewDraft(_t.Customer.Id, vehicleId);
            inv.Lines.Add(TestDatabase.Labor("Inspection", 1m, 50m));
            invoices.SaveDraft(inv);
        }

        Assert.Equal(3, invoices.ListByCustomer(_t.Customer.Id).Count);
        Assert.Equal(2, invoices.ListByVehicle(truck.Id).Count);
        Assert.All(invoices.ListByVehicle(truck.Id), s => Assert.Equal("2015 Ford F-150", s.VehicleDescription));
    }

    [Fact]
    public void Customer_search_matches_plate()
    {
        var found = new CustomerRepository(_t.Db).List("abc1");

        Assert.Equal("Pat Driver", Assert.Single(found).Name);
    }

    [Fact]
    public void Invoice_and_ticket_numbers_are_sequential()
    {
        var invoices = new InvoiceRepository(_t.Db);
        var service = new InvoiceService(_t.Db);
        var a = service.NewDraft(_t.Customer.Id, _t.Vehicle.Id);
        var b = service.NewDraft(_t.Customer.Id, _t.Vehicle.Id);
        invoices.SaveDraft(a);
        invoices.SaveDraft(b);

        Assert.Equal(1001, a.Number);
        Assert.Equal(1002, b.Number);
    }
}
