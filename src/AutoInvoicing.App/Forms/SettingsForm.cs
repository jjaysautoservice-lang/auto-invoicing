namespace AutoInvoicing.App.Forms;

internal sealed class SettingsForm : Form
{
    public SettingsForm(AppServices app, string dbPath)
    {
        var s = app.LoadSettings();
        Ui.SetupDialog(this, "Shop Settings", 560, 470);
        var t = Ui.FieldTable();
        var name = t.AddField("Shop name", Ui.Text(s.ShopName));
        var address = t.AddField("Address", Ui.Text(s.Address, multiline: true), 54);
        var phone = t.AddField("Phone", Ui.Text(s.Phone));
        var email = t.AddField("Email", Ui.Text(s.Email));
        var taxId = t.AddField("Tax / license #", Ui.Text(s.TaxId));
        var rate = t.AddField("Sales tax rate %", Ui.Text(s.TaxRatePercent.ToString("0.###")));
        var taxLabor = t.AddField("Tax labor", new CheckBox { Checked = s.TaxLabor });
        var taxFees = t.AddField("Tax fees / sublet", new CheckBox { Checked = s.TaxFees });
        var labor = t.AddField("Default labor rate", Ui.Text(s.LaborRate.ToString("0.00")));
        var footer = t.AddField("Invoice footer", Ui.Text(s.InvoiceFooter, multiline: true), 54);
        t.AddField("Database file", new TextBox { Text = dbPath, ReadOnly = true });
        Controls.Add(t);
        Controls.Add(Ui.DialogButtons(this, () =>
        {
            s.ShopName = name.Text.Trim();
            s.Address = address.Text.Trim();
            s.Phone = phone.Text.Trim();
            s.Email = email.Text.Trim();
            s.TaxId = taxId.Text.Trim();
            s.TaxRatePercent = Ui.ParseDecimal(rate.Text, "Tax rate");
            s.TaxLabor = taxLabor.Checked;
            s.TaxFees = taxFees.Checked;
            s.LaborRate = Ui.ParseDecimal(labor.Text, "Labor rate");
            s.InvoiceFooter = footer.Text.Trim();
            app.Settings.Save(s);
            return true;
        }));
    }
}
