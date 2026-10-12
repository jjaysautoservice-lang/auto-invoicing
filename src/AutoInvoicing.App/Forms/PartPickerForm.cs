using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

internal sealed class PartPickerForm : Form
{
    private readonly AppServices _app;
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = Ui.Text();
    private readonly TextBox _qty = Ui.Text("1");

    public Part? SelectedPart { get; private set; }
    public decimal Quantity { get; private set; } = 1m;

    public PartPickerForm(AppServices app)
    {
        _app = app;
        Ui.SetupDialog(this, "Add Part from Inventory", 800, 500);

        _grid.Columns.AddRange(
            Ui.Col("Part #", nameof(Part.PartNumber), 70),
            Ui.Col("Description", nameof(Part.Description), 160),
            Ui.Col("Location", nameof(Part.Location), 50),
            Ui.Col("On Hand", nameof(Part.QuantityOnHand), 40, "0.###", right: true),
            Ui.MoneyCol("Price", nameof(Part.Price), 50));
        _grid.DoubleClick += (_, _) => { if (Ui.Try(() => Accept())) { DialogResult = DialogResult.OK; Close(); } };

        var top = Ui.FieldTable();
        top.Dock = DockStyle.Top;
        top.AddField("Search", _search);
        top.AddField("Quantity", _qty);
        _search.TextChanged += (_, _) => Reload();

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(Ui.DialogButtons(this, Accept, "Add"));
        Reload();
        Shown += (_, _) => _search.Focus();
    }

    private void Reload() => _grid.Bind(_app.Parts.List(_search.Text));

    private bool Accept()
    {
        SelectedPart = _grid.Selected<Part>() ?? throw new ArgumentException("Pick a part from the list.");
        Quantity = Ui.ParseDecimal(_qty.Text, "Quantity");
        if (Quantity <= 0m)
            throw new ArgumentException("Quantity must be more than zero.");
        return true;
    }
}
