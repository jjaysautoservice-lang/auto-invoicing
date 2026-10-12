using AutoInvoicing.App.Printing;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

/// <summary>Service history for a customer (all vehicles) or for one vehicle.</summary>
internal sealed class HistoryForm : Form
{
    private readonly AppServices _app;
    private readonly Customer _customer;
    private readonly ComboBox _vehicle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly DataGridView _invoices = Ui.Grid();
    private readonly DataGridView _tickets = Ui.Grid();
    private readonly Label _summary = new() { AutoSize = true, Font = Ui.HeaderFont, Margin = new Padding(12, 8, 3, 3) };
    private List<InvoiceSummary> _current = new();

    private const string AllVehicles = "All vehicles";

    public HistoryForm(AppServices app, Customer customer, Vehicle? vehicle)
    {
        _app = app;
        _customer = customer;
        Text = $"History: {customer.Name}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1150, 720);
        Font = new Font("Segoe UI", 9.5f);

        _invoices.Columns.AddRange(
            Ui.Col("Date", nameof(InvoiceSummary.InvoiceDate), 30, "d"),
            Ui.Col("Invoice #", nameof(InvoiceSummary.Number), 25),
            Ui.Col("Status", nameof(InvoiceSummary.Status), 28),
            Ui.Col("Vehicle", nameof(InvoiceSummary.VehicleDescription), 60),
            Ui.Col("Mileage", nameof(InvoiceSummary.Mileage), 28, "N0", right: true),
            Ui.Col("Work performed", nameof(InvoiceSummary.WorkSummary), 170),
            Ui.MoneyCol("Total", nameof(InvoiceSummary.Total), 35),
            Ui.MoneyCol("Balance", nameof(InvoiceSummary.BalanceDue), 35));
        _invoices.DoubleClick += (_, _) => OpenInvoice();

        _tickets.Columns.AddRange(
            Ui.Col("Opened", nameof(JobTicketSummary.DateOpened), 30, "d"),
            Ui.Col("Ticket #", nameof(JobTicketSummary.Number), 25),
            Ui.Col("Status", nameof(JobTicketSummary.Status), 30),
            Ui.Col("Vehicle", nameof(JobTicketSummary.VehicleDescription), 60),
            Ui.Col("Concern", nameof(JobTicketSummary.Complaint), 200));

        var vehicles = app.Vehicles.ListByCustomer(customer.Id);
        _vehicle.Items.Add(AllVehicles);
        foreach (var v in vehicles)
            _vehicle.Items.Add(v);
        _vehicle.SelectedIndex = vehicle == null ? 0 : 1 + vehicles.FindIndex(v => v.Id == vehicle.Id);
        _vehicle.SelectedIndexChanged += (_, _) => Reload();

        var bar = Ui.ButtonBar();
        bar.Controls.AddRange(new Control[]
        {
            new Label { Text = "Show:", AutoSize = true, Margin = new Padding(3, 9, 3, 3) }, _vehicle,
            Ui.Button("Open Invoice", (_, _) => OpenInvoice()),
            Ui.Button("Print History", (_, _) => PrintHistory()),
            _summary,
        });

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var invPage = new TabPage("Invoices");
        invPage.Controls.Add(_invoices);
        var tickPage = new TabPage("Job tickets");
        tickPage.Controls.Add(_tickets);
        tabs.TabPages.Add(invPage);
        tabs.TabPages.Add(tickPage);

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 28,
            Padding = new Padding(8, 6, 8, 0),
            Text = $"{customer.Name}   {customer.Phone}   {customer.Email}   {customer.Address.Replace(Environment.NewLine, ", ")}",
        };

        Controls.Add(tabs);
        Controls.Add(bar);
        Controls.Add(info);
        Reload();
    }

    private Vehicle? SelectedVehicle => _vehicle.SelectedItem as Vehicle;

    private void Reload()
    {
        var v = SelectedVehicle;
        _current = v == null ? _app.Invoices.ListByCustomer(_customer.Id) : _app.Invoices.ListByVehicle(v.Id);
        _invoices.Bind(_current);
        _tickets.Bind(v == null ? _app.Tickets.ListByCustomer(_customer.Id) : _app.Tickets.ListByVehicle(v.Id));
        var billed = _current.Where(i => i.Status == InvoiceStatus.Finalized).ToList();
        _summary.Text = $"{billed.Count} visits   Total spent {Ui.Money(billed.Sum(i => i.Total))}   Owes {Ui.Money(billed.Sum(i => i.BalanceDue))}";
    }

    private void OpenInvoice()
    {
        if (_invoices.Selected<InvoiceSummary>() is not { } s)
            return;
        using var f = new InvoiceEditForm(_app, _app.Invoices.Get(s.Id)!);
        f.ShowDialog(this);
        Reload();
    }

    private void PrintHistory()
    {
        var v = SelectedVehicle;
        var subtitle = $"{_customer.Name}  {_customer.Phone}" + (v != null ? $"\n{v}  VIN {v.Vin}" : "");
        var cols = new[]
        {
            new TableColumn("Date", 12), new TableColumn("Inv #", 8), new TableColumn("Vehicle", 20),
            new TableColumn("Miles", 10, StringAlignment.Far), new TableColumn("Work performed", 36),
            new TableColumn("Total", 14, StringAlignment.Far),
        };
        var rows = _current.Where(i => i.Status != InvoiceStatus.Void).Select(i => (IReadOnlyList<string>)new[]
        {
            i.InvoiceDate.ToString("d"), i.Number.ToString(), i.VehicleDescription,
            i.Mileage?.ToString("N0") ?? "", i.WorkSummary, Ui.Money(i.Total) + (i.Status == InvoiceStatus.Draft ? " (est.)" : ""),
        });
        PrintHelper.Preview(this, Documents.Table(v == null ? "Customer Service History" : "Vehicle Service History",
            subtitle, cols, rows, _app.LoadSettings(), _summary.Text));
    }
}
