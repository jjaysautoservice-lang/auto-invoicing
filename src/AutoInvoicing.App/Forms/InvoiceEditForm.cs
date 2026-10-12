using AutoInvoicing.App.Printing;
using AutoInvoicing.Core;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

internal sealed class InvoiceEditForm : Form
{
    private readonly AppServices _app;
    private Invoice _inv;
    private Customer _customer;
    private Vehicle _vehicle;

    private readonly Label _header = new() { AutoSize = true, Font = Ui.HeaderFont, Padding = new Padding(8) };
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short };
    private readonly TextBox _mileage = Ui.Text();
    private readonly TextBox _taxRate = Ui.Text();
    private readonly TextBox _notes = Ui.Text(multiline: true);
    private readonly LineItemsEditor _lines;
    private readonly Label _totals = new() { AutoSize = true, Font = new Font("Consolas", 10f), Padding = new Padding(8) };
    private readonly DataGridView _payments = Ui.Grid();
    private readonly FlowLayoutPanel _actions = Ui.ButtonBar(DockStyle.Bottom);
    private readonly Button _save, _finalize, _void, _delete, _addPayment, _removePayment, _change;

    public InvoiceEditForm(AppServices app, Invoice inv)
    {
        _app = app;
        _inv = inv;
        _customer = app.Customers.Get(inv.CustomerId)!;
        _vehicle = app.Vehicles.Get(inv.VehicleId)!;
        Text = "Invoice";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1150, 800);
        Font = new Font("Segoe UI", 9.5f);

        _lines = new LineItemsEditor(app) { Dock = DockStyle.Fill };
        _lines.LinesChanged += (_, _) => UpdateTotals();
        _taxRate.TextChanged += (_, _) => UpdateTotals();

        var fields = Ui.FieldTable();
        fields.ColumnCount = 4;
        fields.ColumnStyles.Clear();
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.Dock = DockStyle.Top;
        AddPair(fields, 0, 0, "Date", _date);
        AddPair(fields, 0, 2, "Mileage", _mileage);
        AddPair(fields, 1, 0, "Tax rate %", _taxRate);
        _change = Ui.Button("Change Customer/Vehicle", (_, _) => ChangeCustomer());
        fields.Controls.Add(_change, 3, 1);
        AddPair(fields, 2, 0, "Notes", _notes);
        fields.SetColumnSpan(_notes, 3);
        _notes.Height = 54;

        _payments.Columns.AddRange(
            Ui.Col("Date", nameof(Payment.Date), 60, "d"),
            Ui.Col("Method", nameof(Payment.Method), 60),
            Ui.Col("Reference", nameof(Payment.Reference), 70),
            Ui.MoneyCol("Amount", nameof(Payment.Amount)));
        _addPayment = Ui.Button("Take Payment", (_, _) => TakePayment());
        _removePayment = Ui.Button("Remove Payment", (_, _) => RemovePayment());
        var payBar = Ui.ButtonBar(DockStyle.Bottom);
        payBar.Controls.AddRange(new Control[] { _addPayment, _removePayment });
        var payBox = new GroupBox { Text = "Payments", Dock = DockStyle.Fill };
        payBox.Controls.Add(_payments);
        payBox.Controls.Add(payBar);

        var bottom = Ui.Split(0.5f, DockStyle.Bottom);
        bottom.Height = 220;
        bottom.Panel1.Controls.Add(payBox);
        bottom.Panel2.Controls.Add(_totals);

        _save = Ui.Button("Save", (_, _) => Ui.Try(Save));
        _finalize = Ui.Button("Finalize (deduct parts)", (_, _) => FinalizeInvoice());
        _void = Ui.Button("Void", (_, _) => VoidInvoice());
        _delete = Ui.Button("Delete Draft", (_, _) => DeleteDraft());
        _actions.Controls.AddRange(new Control[]
        {
            _save, _finalize,
            Ui.Button("Print Preview", (_, _) => PrintDoc(preview: true)),
            Ui.Button("Print", (_, _) => PrintDoc(preview: false)),
            _void, _delete,
            Ui.Button("Close", (_, _) => Close()),
        });

        var linesBox = new GroupBox { Text = "Labor, Parts and Fees", Dock = DockStyle.Fill };
        linesBox.Controls.Add(_lines);

        Controls.Add(linesBox);
        Controls.Add(fields);
        Controls.Add(_header);
        Controls.Add(bottom);
        Controls.Add(_actions);
        LoadInvoice();
    }

    private static void AddPair(TableLayoutPanel t, int row, int col, string label, Control control)
    {
        while (t.RowCount <= row)
        {
            t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        t.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 8, 3) }, col, row);
        control.Dock = DockStyle.Fill;
        t.Controls.Add(control, col + 1, row);
    }

    private bool IsDraft => _inv.Status == InvoiceStatus.Draft;

    private void LoadInvoice()
    {
        _date.Value = _inv.InvoiceDate;
        _mileage.Text = _inv.Mileage?.ToString() ?? "";
        _taxRate.Text = _inv.TaxRatePercent.ToString("0.###");
        _notes.Text = _inv.Notes;
        _lines.Lines = _inv.Lines;
        _payments.Bind(_inv.Payments);
        ApplyState();
    }

    private void ApplyState()
    {
        var label = _inv.Id == 0 ? "New invoice" : $"{(IsDraft ? "Draft invoice" : "Invoice")} #{_inv.Number}";
        _header.Text = $"{label}   {_inv.Status.ToString().ToUpperInvariant()}   {_customer.Name}, {_vehicle}";
        if (_inv.Status == InvoiceStatus.Void)
            _header.Text += $"   (void: {_inv.VoidReason})";
        Text = label;
        foreach (var c in new Control[] { _date, _mileage, _taxRate, _notes, _change })
            c.Enabled = IsDraft;
        _lines.ReadOnlyMode = !IsDraft;
        _save.Visible = IsDraft;
        _finalize.Visible = IsDraft;
        _delete.Visible = IsDraft && _inv.Id != 0;
        _void.Visible = _inv.Status != InvoiceStatus.Void && _inv.Id != 0;
        _addPayment.Enabled = _inv.Status != InvoiceStatus.Void && _inv.Id != 0;
        _removePayment.Enabled = _addPayment.Enabled;
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        var rate = Ui.TryParseDecimal(_taxRate.Text, out var r) && r >= 0m ? r : _inv.TaxRatePercent;
        var t = InvoiceCalculator.Calculate(_lines.CurrentLines, rate, _inv.AmountPaid);
        _totals.Text =
            $"""
            Labor      {Ui.Money(t.LaborTotal),14}
            Parts      {Ui.Money(t.PartsTotal),14}
            Fees       {Ui.Money(t.FeesTotal),14}
            Subtotal   {Ui.Money(t.Subtotal),14}
            Tax        {Ui.Money(t.Tax),14}
            TOTAL      {Ui.Money(t.Total),14}
            Paid       {Ui.Money(t.AmountPaid),14}
            BALANCE    {Ui.Money(_inv.Status == InvoiceStatus.Void ? 0m : t.BalanceDue),14}
            """;
    }

    /// <summary>Copies the screen into the invoice and saves it as a draft.</summary>
    private void Save()
    {
        if (!IsDraft)
            return;
        _inv.InvoiceDate = _date.Value.Date;
        _inv.Mileage = Ui.ParseOptionalInt(_mileage.Text, "Mileage");
        _inv.TaxRatePercent = Ui.ParseDecimal(_taxRate.Text, "Tax rate");
        _inv.Notes = _notes.Text.Trim();
        _inv.Lines = _lines.Lines;
        foreach (var l in _inv.Lines.Where(l => string.IsNullOrWhiteSpace(l.Description)))
            throw new ArgumentException("Every line needs a description.");
        _app.Invoices.SaveDraft(_inv);
        Reload();
    }

    private void Reload()
    {
        _inv = _app.Invoices.Get(_inv.Id)!;
        LoadInvoice();
    }

    private void FinalizeInvoice()
    {
        if (!Ui.Try(Save))
            return;
        if (!Ui.Confirm($"Finalize invoice #{_inv.Number} for {Ui.Money(_inv.Totals.Total)}?\n\nParts on this invoice will be taken out of inventory and the invoice will be locked."))
            return;
        Ui.Try(() =>
        {
            var warnings = _app.InvoiceService.Finalize(_inv.Id);
            Reload();
            if (warnings.Count > 0)
                Ui.Info("Inventory check:\n\n" + string.Join("\n", warnings));
            if (Ui.Confirm("Invoice finalized. Print it now?"))
                PrintDoc(preview: false);
        });
    }

    private void VoidInvoice()
    {
        var reason = Prompt.Show(this, "Void Invoice", "Reason for voiding:");
        if (reason == null)
            return;
        Ui.Try(() =>
        {
            _app.InvoiceService.Void(_inv.Id, reason);
            Reload();
        });
    }

    private void DeleteDraft()
    {
        if (!Ui.Confirm("Delete this draft invoice?"))
            return;
        if (Ui.Try(() => _app.Invoices.DeleteDraft(_inv.Id)))
            Close();
    }

    private void ChangeCustomer()
    {
        using var picker = new CustomerVehiclePickerForm(_app, "Change Customer / Vehicle");
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        _customer = picker.Customer!;
        _vehicle = picker.Vehicle!;
        _inv.CustomerId = _customer.Id;
        _inv.VehicleId = _vehicle.Id;
        ApplyState();
    }

    private void TakePayment()
    {
        using var f = new PaymentForm(_inv.Totals.BalanceDue);
        if (f.ShowDialog(this) != DialogResult.OK)
            return;
        if (Ui.Try(() => _app.InvoiceService.AddPayment(_inv.Id, f.Amount, f.Method, f.Date, f.Reference)))
            Reload();
    }

    private void RemovePayment()
    {
        if (_payments.Selected<Payment>() is { } p && Ui.Confirm($"Remove the {Ui.Money(p.Amount)} {p.Method} payment?"))
        {
            _app.InvoiceService.DeletePayment(p.Id);
            Reload();
        }
    }

    private void PrintDoc(bool preview)
    {
        if (IsDraft && !Ui.Try(Save))
            return;
        var doc = Documents.Invoice(_inv, _customer, _vehicle, _app.LoadSettings());
        if (preview)
            PrintHelper.Preview(this, doc);
        else
            PrintHelper.Print(this, doc);
    }
}

internal sealed class PaymentForm : Form
{
    public decimal Amount { get; private set; }
    public string Method { get; private set; } = "";
    public string Reference { get; private set; } = "";
    public DateTime Date { get; private set; }

    public PaymentForm(decimal balance)
    {
        Ui.SetupDialog(this, "Take Payment", 380, 220);
        var t = Ui.FieldTable();
        var amount = t.AddField("Amount", Ui.Text(balance > 0 ? balance.ToString("0.00") : ""));
        var method = t.AddField("Method", new ComboBox { DropDownStyle = ComboBoxStyle.DropDown });
        method.Items.AddRange(new object[] { "Cash", "Card", "Check", "Financing", "Other" });
        method.SelectedIndex = 1;
        var reference = t.AddField("Check # / Ref", Ui.Text());
        var date = t.AddField("Date", new DateTimePicker { Format = DateTimePickerFormat.Short });
        Controls.Add(t);
        Controls.Add(Ui.DialogButtons(this, () =>
        {
            Amount = Ui.ParseDecimal(amount.Text, "Amount");
            if (Amount == 0m)
                throw new ArgumentException("Enter an amount.");
            Method = method.Text.Trim();
            Reference = reference.Text.Trim();
            Date = date.Value.Date;
            return true;
        }, "Record"));
    }
}

/// <summary>A one-line text prompt.</summary>
internal static class Prompt
{
    public static string? Show(IWin32Window owner, string title, string label)
    {
        using var f = new Form();
        Ui.SetupDialog(f, title, 420, 120);
        var t = Ui.FieldTable();
        var box = t.AddField(label, Ui.Text());
        string? result = null;
        f.Controls.Add(t);
        f.Controls.Add(Ui.DialogButtons(f, () =>
        {
            if (box.Text.Trim().Length == 0)
                throw new ArgumentException("Please enter a value.");
            result = box.Text.Trim();
            return true;
        }, "OK"));
        return f.ShowDialog(owner) == DialogResult.OK ? result : null;
    }
}
