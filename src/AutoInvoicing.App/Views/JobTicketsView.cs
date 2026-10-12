using AutoInvoicing.App.Forms;
using AutoInvoicing.App.Printing;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Views;

internal sealed class JobTicketsView : UserControl, IRefreshable
{
    private readonly AppServices _app;
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text();
    private readonly CheckBox _openOnly = new() { Text = "Open tickets only", Checked = true, AutoSize = true };

    public string Title => "Job Tickets";

    public JobTicketsView(AppServices app)
    {
        _app = app;
        _grid.Columns.AddRange(
            Ui.Col("Ticket #", nameof(JobTicketSummary.Number), 30),
            Ui.Col("Opened", nameof(JobTicketSummary.DateOpened), 35, "d"),
            Ui.Col("Status", nameof(JobTicketSummary.Status), 35),
            Ui.Col("Customer", nameof(JobTicketSummary.CustomerName), 70),
            Ui.Col("Vehicle", nameof(JobTicketSummary.VehicleDescription), 70),
            Ui.Col("Tech", nameof(JobTicketSummary.Technician), 40),
            Ui.Col("Concern", nameof(JobTicketSummary.Complaint), 140));
        _grid.DoubleClick += (_, _) => Open();

        var bar = Ui.ButtonBar();
        bar.Controls.AddRange(new Control[]
        {
            Ui.Button("New Job Ticket", (_, _) => New()),
            Ui.Button("Open", (_, _) => Open()),
            Ui.Button("Print", (_, _) => Print()),
            Ui.Button("Create Invoice", (_, _) => CreateInvoice()),
            new Label { Text = "   Search:", AutoSize = true, Margin = new Padding(3, 9, 3, 3) },
        });
        _search.Width = 220;
        _search.Margin = new Padding(3, 6, 3, 3);
        _openOnly.Margin = new Padding(10, 8, 3, 3);
        bar.Controls.Add(_search);
        bar.Controls.Add(_openOnly);
        _search.TextChanged += (_, _) => RefreshData();
        _openOnly.CheckedChanged += (_, _) => RefreshData();

        Controls.Add(_grid);
        Controls.Add(bar);
    }

    public void RefreshData() => _grid.Bind(_app.Tickets.List(_openOnly.Checked, _search.Text));

    private void New()
    {
        using var picker = new CustomerVehiclePickerForm(_app, "New Job Ticket: pick customer and vehicle");
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        var ticket = new JobTicket { CustomerId = picker.Customer!.Id, VehicleId = picker.Vehicle!.Id };
        using var f = new JobTicketEditForm(_app, ticket);
        f.ShowDialog(this);
        RefreshData();
    }

    private void Open()
    {
        if (_grid.Selected<JobTicketSummary>() is not { } s)
            return;
        using var f = new JobTicketEditForm(_app, _app.Tickets.Get(s.Id)!);
        f.ShowDialog(this);
        RefreshData();
    }

    private void Print()
    {
        if (_grid.Selected<JobTicketSummary>() is not { } s)
            return;
        var t = _app.Tickets.Get(s.Id)!;
        PrintHelper.Preview(this, Documents.JobTicket(t, _app.Customers.Get(t.CustomerId)!, _app.Vehicles.Get(t.VehicleId)!, _app.LoadSettings()));
    }

    private void CreateInvoice()
    {
        if (_grid.Selected<JobTicketSummary>() is not { } s)
            return;
        if (s.Status == JobStatus.Invoiced && !Ui.Confirm("This ticket was already invoiced. Create another invoice?"))
            return;
        Invoice? inv = null;
        if (!Ui.Try(() => inv = _app.InvoiceService.CreateFromJobTicket(s.Id)))
            return;
        using var f = new InvoiceEditForm(_app, _app.Invoices.Get(inv!.Id)!);
        f.ShowDialog(this);
        RefreshData();
    }
}
