using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

internal sealed class PartEditForm : Form
{
    public PartEditForm(AppServices app, Part part)
    {
        var isNew = part.Id == 0;
        Ui.SetupDialog(this, isNew ? "New Part" : "Edit Part", 460, 420);
        var t = Ui.FieldTable();
        var number = t.AddField("Part #", Ui.Text(part.PartNumber));
        var desc = t.AddField("Description", Ui.Text(part.Description));
        var vendor = t.AddField("Vendor", Ui.Text(part.Vendor));
        var location = t.AddField("Bin / location", Ui.Text(part.Location));
        var cost = t.AddField("Cost", Ui.Text(part.Cost.ToString("0.00##")));
        var price = t.AddField("Sell price", Ui.Text(part.Price.ToString("0.00##")));
        var qty = t.AddField(isNew ? "Opening qty" : "On hand", Ui.Text(Ui.Qty(part.QuantityOnHand)));
        qty.ReadOnly = !isNew; // after creation, stock only changes through Receive / Adjust so it's logged
        var reorder = t.AddField("Reorder at", Ui.Text(Ui.Qty(part.ReorderLevel)));
        var taxable = t.AddField("Taxable", new CheckBox { Checked = part.Taxable });
        var active = t.AddField("Active", new CheckBox { Checked = part.Active });
        Controls.Add(t);
        Controls.Add(Ui.DialogButtons(this, () =>
        {
            part.PartNumber = number.Text.Trim();
            part.Description = desc.Text.Trim();
            part.Vendor = vendor.Text.Trim();
            part.Location = location.Text.Trim();
            part.Cost = Ui.ParseDecimal(cost.Text, "Cost");
            part.Price = Ui.ParseDecimal(price.Text, "Sell price");
            if (isNew)
                part.QuantityOnHand = Ui.ParseDecimal(qty.Text, "Quantity");
            part.ReorderLevel = Ui.ParseDecimal(reorder.Text, "Reorder level");
            part.Taxable = taxable.Checked;
            part.Active = active.Checked;
            if (part.Price < part.Cost && !Ui.Confirm("Sell price is below cost. Save anyway?"))
                return false;
            var existing = app.Parts.GetByNumber(part.PartNumber);
            if (existing != null && existing.Id != part.Id)
                throw new ArgumentException($"Part # {part.PartNumber} already exists.");
            app.Parts.Save(part);
            return true;
        }));
    }
}

internal sealed class StockAdjustForm : Form
{
    public StockAdjustForm(AppServices app, Part part)
    {
        Ui.SetupDialog(this, $"Receive / Adjust: {part.PartNumber}", 460, 220);
        var t = Ui.FieldTable();
        t.AddField("On hand now", new Label { Text = Ui.Qty(part.QuantityOnHand), AutoSize = true });
        var mode = t.AddField("Action", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList });
        mode.Items.AddRange(new object[] { "Receive stock (+)", "Remove stock (-)", "Set counted quantity" });
        mode.SelectedIndex = 0;
        var qty = t.AddField("Quantity", Ui.Text());
        var reason = t.AddField("Reason / PO #", Ui.Text());
        Controls.Add(t);
        Controls.Add(Ui.DialogButtons(this, () =>
        {
            var q = Ui.ParseDecimal(qty.Text, "Quantity");
            var change = mode.SelectedIndex switch
            {
                0 => q,
                1 => -q,
                _ => q - part.QuantityOnHand,
            };
            var why = reason.Text.Trim();
            if (why.Length == 0)
                why = mode.SelectedIndex switch { 0 => "Received", 1 => "Removed", _ => "Physical count" };
            if (change == 0m)
                return true;
            app.Parts.AdjustStock(part.Id, change, why);
            return true;
        }));
    }
}

internal sealed class PartHistoryForm : Form
{
    public PartHistoryForm(AppServices app, Part part)
    {
        Ui.SetupDialog(this, $"Stock history: {part.PartNumber} {part.Description}", 700, 450);
        var grid = Ui.Grid();
        grid.Columns.AddRange(
            Ui.Col("Date", nameof(InventoryTransaction.Date), 60, "g"),
            Ui.Col("Change", nameof(InventoryTransaction.QuantityChange), 40, "+0.###;-0.###", right: true),
            Ui.Col("Reason", nameof(InventoryTransaction.Reason), 150));
        grid.Bind(app.Parts.Transactions(part.Id));
        Controls.Add(grid);
    }
}
