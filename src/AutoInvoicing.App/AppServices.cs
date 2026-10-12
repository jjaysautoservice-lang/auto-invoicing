using AutoInvoicing.Core.Data;
using AutoInvoicing.Core.Models;
using AutoInvoicing.Core.Services;

namespace AutoInvoicing.App;

/// <summary>One place the forms get their repositories and services from.</summary>
public sealed class AppServices
{
    public AppServices(Database db)
    {
        Db = db;
        Customers = new CustomerRepository(db);
        Vehicles = new VehicleRepository(db);
        Parts = new PartRepository(db);
        Tickets = new JobTicketRepository(db);
        Invoices = new InvoiceRepository(db);
        Settings = new SettingsRepository(db);
        InvoiceService = new InvoiceService(db);
        Reports = new ReportService(db);
    }

    public Database Db { get; }
    public CustomerRepository Customers { get; }
    public VehicleRepository Vehicles { get; }
    public PartRepository Parts { get; }
    public JobTicketRepository Tickets { get; }
    public InvoiceRepository Invoices { get; }
    public SettingsRepository Settings { get; }
    public InvoiceService InvoiceService { get; }
    public ReportService Reports { get; }

    public ShopSettings LoadSettings() => Settings.Load();
}
