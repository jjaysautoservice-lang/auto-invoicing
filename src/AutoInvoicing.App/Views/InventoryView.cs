using AutoInvoicing.App.Forms;
using AutoInvoicing.App.Printing;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Views;

internal sealed class InventoryView : UserControl, IRefreshable
{
    private readonly AppServices _app;
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text();
    private readonly CheckBox _lowOnly = new() { Text = "Low stock only", AutoSize = true };
    private readonly CheckBox _inactive = new() { Text = "Show inactive", AutoSize = true };
    private readonly Label _value = new() { AutoSize = true, Margin = new Padding(12, 9, 3, 3) };

    public string Title => "Inventory";

    public InventoryView(AppServices app)
    {
        _app = app;
        _grid.Columns.AddRange(
            Ui.Col("Part #", nameof(Part.PartNumber), 60),
            Ui.Col("Description", nameof(Part.Description), 150),
            Ui.Col("Vendor", nameof(Part.Vendor), 50),
            Ui.Col("Location", nameof(Part.Location), 40),
            Ui.Col("On Hand", nameof(Part.QuantityOnHand), 35, "0.###", right: true),
            Ui.Col("Reorder At", nameof(Part.ReorderLevel), 35, "0.###", right: true),
            Ui.MoneyCol("Cost", nameof(Part.Cost), 40),
            Ui.MoneyCol("Price", nameof(Part.Price), 40));
        _grid.DoubleClick += (_, _) => Edit();
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is Part p && e.CellStyle != null)
            {
                if (p.QuantityOnHand < 0m)
                    e.CellStyle.BackColor = Color.MistyRose;
                else if (p.IsLowStock)
                    e.CellStyle.BackColor = Color.LightYellow;
                if (!p.Active)
                    e.CellStyle.ForeColor = Color.Gray;
            }
        };

        var bar = Ui.ButtonBar();
        bar.Controls.AddRange(new Control[]
        {
            Ui.Button("New Part", (_, _) => New()),
            Ui.Button("Edit", (_, _) => Edit()),
            Ui.Button("Receive / Adjust Stock", (_, _) => Adjust()),
            Ui.Button("Stock History", (_, _) => History()),
            Ui.Button("Print List", (_, _) => PrintList()),
            new Label { Text = "  Search:", AutoSize = true, Margin = new Padding(3, 9, 3, 3) }, _search,
            _lowOnly, _inactive, _value,
        });
        _search.Width = 200;
        foreach (Control c in new Control[] { _search, _lowOnly, _inactive })
            c.Margin = new Padding(6, 7, 3, 3);
        _search.TextChanged += (_, _) => RefreshData();
        _lowOnly.CheckedChanged += (_, _) => RefreshData();
        _inactive.CheckedChanged += (_, _) => RefreshData();

        Controls.Add(_grid);
        Controls.Add(bar);
    }

    private List<Part> Current => _app.Parts.List(_search.Text, _inactive.Checked, _lowOnly.Checked);

    public void RefreshData()
    {
        var parts = _grid.Bind(Current);
        var value = parts.Where(p => p.QuantityOnHand > 0m).Sum(p => Core.Money.Round(p.QuantityOnHand * p.Cost));
        _value.Text = $"{parts.Count} parts   Stock value at cost: {Ui.Money(value)}";
    }

    private void New()
    {
        using var f = new PartEditForm(_app, new Part());
        if (f.ShowDialog(this) == DialogResult.OK)
            RefreshData();
    }

    private void Edit()
    {
        if (_grid.Selected<Part>() is not { } p)
            return;
        using var f = new PartEditForm(_app, _app.Parts.Get(p.Id)!);
        if (f.ShowDialog(this) == DialogResult.OK)
            RefreshData();
    }

    private void Adjust()
    {
        if (_grid.Selected<Part>() is not { } p)
            return;
        using var f = new StockAdjustForm(_app, _app.Parts.Get(p.Id)!);
        if (f.ShowDialog(this) == DialogResult.OK)
            RefreshData();
    }

    private void History()
    {
        if (_grid.Selected<Part>() is not { } p)
            return;
        using var f = new PartHistoryForm(_app, p);
        f.ShowDialog(this);
    }

    private void PrintList()
    {
        var cols = new[]
        {
            new TableColumn("Part #", 16), new TableColumn("Description", 34), new TableColumn("Location", 12),
            new TableColumn("On Hand", 10, StringAlignment.Far), new TableColumn("Reorder", 10, StringAlignment.Far),
            new TableColumn("Cost", 12, StringAlignment.Far), new TableColumn("Counted", 10),
        };
        var rows = Current.Select(p => (IReadOnlyList<string>)new[]
        {
            p.PartNumber, p.Description, p.Location, Ui.Qty(p.QuantityOnHand), Ui.Qty(p.ReorderLevel), Ui.Money(p.Cost), "______",
        });
        PrintHelper.Preview(this, Documents.Table(_lowOnly.Checked ? "Reorder List" : "Inventory List",
            _value.Text, cols, rows, _app.LoadSettings()));
    }
}
