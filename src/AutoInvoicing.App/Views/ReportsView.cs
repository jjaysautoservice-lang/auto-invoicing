using AutoInvoicing.App.Printing;
using AutoInvoicing.Core.Models;
using AutoInvoicing.Core.Services;

namespace AutoInvoicing.App.Views;

internal sealed class ReportsView : UserControl, IRefreshable
{
    private readonly AppServices _app;
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Long, Width = 230 };
    private readonly Label _title = new() { AutoSize = true, Font = Ui.HeaderFont, Margin = new Padding(12, 8, 3, 3) };
    private readonly DataGridView _summary = Ui.Grid();
    private readonly DataGridView _byDay = Ui.Grid();
    private readonly DataGridView _parts = Ui.Grid();
    private readonly DataGridView _invoices = Ui.Grid();
    private SalesReport? _report;

    public string Title => "Reports";

    private sealed record SummaryRow(string Item, string Amount);

    public ReportsView(AppServices app)
    {
        _app = app;
        _kind.Items.AddRange(Enum.GetValues<PeriodKind>().Cast<object>().ToArray());
        _kind.SelectedIndex = 0;
        _kind.SelectedIndexChanged += (_, _) => RefreshData();
        _date.ValueChanged += (_, _) => RefreshData();

        _summary.Columns.AddRange(Ui.Col("", nameof(SummaryRow.Item), 60), Ui.Col("", nameof(SummaryRow.Amount), 40, right: true));
        _summary.ColumnHeadersVisible = false;
        _byDay.Columns.AddRange(
            Ui.Col("Date", nameof(DailyRow.Date), 50, "ddd MMM d"),
            Ui.Col("Invoices", nameof(DailyRow.InvoiceCount), 25, right: true),
            Ui.MoneyCol("Sales", nameof(DailyRow.Sales), 40),
            Ui.MoneyCol("Collected", nameof(DailyRow.Collected), 40));
        _parts.Columns.AddRange(
            Ui.Col("Part #", nameof(PartSalesRow.PartNumber), 50),
            Ui.Col("Description", nameof(PartSalesRow.Description), 100),
            Ui.Col("Qty", nameof(PartSalesRow.Quantity), 25, "0.###", right: true),
            Ui.MoneyCol("Sales", nameof(PartSalesRow.Sales), 40),
            Ui.MoneyCol("Margin", nameof(PartSalesRow.Margin), 40));
        _invoices.Columns.AddRange(
            Ui.Col("#", nameof(InvoiceSummary.Number), 25),
            Ui.Col("Date", nameof(InvoiceSummary.InvoiceDate), 30, "d"),
            Ui.Col("Customer", nameof(InvoiceSummary.CustomerName), 60),
            Ui.Col("Vehicle", nameof(InvoiceSummary.VehicleDescription), 60),
            Ui.MoneyCol("Total", nameof(InvoiceSummary.Total), 35),
            Ui.MoneyCol("Balance", nameof(InvoiceSummary.BalanceDue), 35));

        var bar = Ui.ButtonBar();
        bar.Controls.AddRange(new Control[]
        {
            new Label { Text = "Report:", AutoSize = true, Margin = new Padding(3, 9, 3, 3) }, _kind,
            new Label { Text = "For:", AutoSize = true, Margin = new Padding(8, 9, 3, 3) }, _date,
            Ui.Button("Today", (_, _) => _date.Value = DateTime.Today),
            Ui.Button("Print Preview", (_, _) => Print(preview: true)),
            Ui.Button("Print", (_, _) => Print(preview: false)),
            _title,
        });
        _kind.Margin = _date.Margin = new Padding(3, 6, 3, 3);

        GroupBox Box(string title, Control c)
        {
            var g = new GroupBox { Text = title, Dock = DockStyle.Fill };
            g.Controls.Add(c);
            return g;
        }
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.Controls.Add(Box("Summary", _summary), 0, 0);
        layout.Controls.Add(Box("By day", _byDay), 1, 0);
        layout.Controls.Add(Box("Top parts", _parts), 0, 1);
        layout.Controls.Add(Box("Invoices in period", _invoices), 1, 1);

        Controls.Add(layout);
        Controls.Add(bar);
    }

    public void RefreshData()
    {
        if (_kind.SelectedItem is not PeriodKind kind)
            return;
        var r = _report = _app.Reports.Build(kind, _date.Value);
        _title.Text = r.Period.Title;
        var rows = new List<SummaryRow>
        {
            new("Invoices", r.InvoiceCount.ToString()),
            new("Labor", Ui.Money(r.Labor)),
            new("Parts", Ui.Money(r.Parts)),
            new("Fees / sublet", Ui.Money(r.Fees)),
            new("Subtotal", Ui.Money(r.Subtotal)),
            new("Sales tax", Ui.Money(r.Tax)),
            new("Total sales", Ui.Money(r.Total)),
            new("Parts cost", Ui.Money(r.PartsCost)),
            new("Parts margin", Ui.Money(r.PartsMargin)),
            new("Gross profit (excl. tax)", Ui.Money(r.GrossProfit)),
            new("Average ticket", Ui.Money(r.AverageTicket)),
            new("Collected", Ui.Money(r.Collected)),
        };
        rows.AddRange(r.CollectedByMethod.Select(kv => new SummaryRow("    " + kv.Key, Ui.Money(kv.Value))));
        rows.Add(new("Still owed on these invoices", Ui.Money(r.OutstandingFromPeriod)));
        _summary.Bind(rows);
        _byDay.Bind(r.ByDay);
        _parts.Bind(r.TopParts);
        _invoices.Bind(r.Invoices);
    }

    private void Print(bool preview)
    {
        if (_report == null)
            RefreshData();
        var doc = Documents.SalesReport(_report!, _app.LoadSettings());
        if (preview)
            PrintHelper.Preview(this, doc);
        else
            PrintHelper.Print(this, doc);
    }
}
