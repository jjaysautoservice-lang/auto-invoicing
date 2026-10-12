using AutoInvoicing.App.Forms;
using AutoInvoicing.App.Printing;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Views;

internal sealed class InvoicesView : UserControl, IRefreshable
{
    private readonly AppServices _app;
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text();
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 110 };

    public string Title => "Invoices";

    public InvoicesView(AppServices app)
    {
        _app = app;
        _from.Value = DateTime.Today.AddDays(-30);
        _grid.Columns.AddRange(
            Ui.Col("Invoice #", nameof(InvoiceSummary.Number), 30),
            Ui.Col("Date", nameof(InvoiceSummary.InvoiceDate), 35, "d"),
            Ui.Col("Status", nameof(InvoiceSummary.Status), 35),
            Ui.Col("Customer", nameof(InvoiceSummary.CustomerName), 70),
            Ui.Col("Vehicle", nameof(InvoiceSummary.VehicleDescription), 70),
            Ui.Col("Work", nameof(InvoiceSummary.WorkSummary), 120),
            Ui.MoneyCol("Total", nameof(InvoiceSummary.Total), 40),
            Ui.MoneyCol("Balance", nameof(InvoiceSummary.BalanceDue), 40));
        _grid.DoubleClick += (_, _) => Open();
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is InvoiceSummary s && e.CellStyle != null)
            {
                if (s.Status == InvoiceStatus.Void)
                    e.CellStyle.ForeColor = Color.Gray;
                else if (s.Status == InvoiceStatus.Finalized && s.BalanceDue > 0m)
                    e.CellStyle.ForeColor = Color.DarkRed;
            }
        };

        var bar = Ui.ButtonBar();
        bar.Controls.AddRange(new Control[]
        {
            Ui.Button("New Invoice", (_, _) => New()),
            Ui.Button("Open", (_, _) => Open()),
            Ui.Button("Print", (_, _) => Print()),
            new Label { Text = "   From", AutoSize = true, Margin = new Padding(3, 9, 3, 3) }, _from,
            new Label { Text = "To", AutoSize = true, Margin = new Padding(3, 9, 3, 3) }, _to,
            new Label { Text = "Search:", AutoSize = true, Margin = new Padding(10, 9, 3, 3) }, _search,
        });
        _search.Width = 200;
        foreach (Control c in new Control[] { _from, _to, _search })
            c.Margin = new Padding(3, 6, 3, 3);
        _search.TextChanged += (_, _) => RefreshData();
        _from.ValueChanged += (_, _) => RefreshData();
        _to.ValueChanged += (_, _) => RefreshData();

        Controls.Add(_grid);
        Controls.Add(bar);
    }

    public void RefreshData() => _grid.Bind(_app.Invoices.List(_from.Value.Date, _to.Value.Date, _search.Text));

    private void New()
    {
        using var picker = new CustomerVehiclePickerForm(_app, "New Invoice: pick customer and vehicle");
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        using var f = new InvoiceEditForm(_app, _app.InvoiceService.NewDraft(picker.Customer!.Id, picker.Vehicle!.Id));
        f.ShowDialog(this);
        RefreshData();
    }

    private void Open()
    {
        if (_grid.Selected<InvoiceSummary>() is not { } s)
            return;
        using var f = new InvoiceEditForm(_app, _app.Invoices.Get(s.Id)!);
        f.ShowDialog(this);
        RefreshData();
    }

    private void Print()
    {
        if (_grid.Selected<InvoiceSummary>() is not { } s)
            return;
        var inv = _app.Invoices.Get(s.Id)!;
        PrintHelper.Preview(this, Documents.Invoice(inv, _app.Customers.Get(inv.CustomerId)!, _app.Vehicles.Get(inv.VehicleId)!, _app.LoadSettings()));
    }
}
