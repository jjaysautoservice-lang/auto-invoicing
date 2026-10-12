using AutoInvoicing.App.Printing;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

internal sealed class JobTicketEditForm : Form
{
    private readonly AppServices _app;
    private JobTicket _ticket;
    private Customer _customer;
    private Vehicle _vehicle;

    private readonly Label _header = new() { AutoSize = true, Font = Ui.HeaderFont, Padding = new Padding(8) };
    private readonly DateTimePicker _opened = new() { Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker _promised = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true };
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _mileage = Ui.Text();
    private readonly TextBox _tech = Ui.Text();
    private readonly TextBox _complaint = Ui.Text(multiline: true);
    private readonly TextBox _cause = Ui.Text(multiline: true);
    private readonly TextBox _correction = Ui.Text(multiline: true);
    private readonly TextBox _notes = Ui.Text(multiline: true);
    private readonly LineItemsEditor _lines;

    public JobTicketEditForm(AppServices app, JobTicket ticket)
    {
        _app = app;
        _ticket = ticket;
        _customer = app.Customers.Get(ticket.CustomerId)!;
        _vehicle = app.Vehicles.Get(ticket.VehicleId)!;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1100, 820);
        Font = new Font("Segoe UI", 9.5f);
        _status.Items.AddRange(Enum.GetValues<JobStatus>().Cast<object>().ToArray());

        var top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 6, AutoSize = true, Padding = new Padding(8) };
        for (var i = 0; i < 3; i++)
        {
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        }
        void Pair(int row, int col, string label, Control c)
        {
            top.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 8, 3) }, col, row);
            c.Dock = DockStyle.Fill;
            top.Controls.Add(c, col + 1, row);
        }
        Pair(0, 0, "Opened", _opened);
        Pair(0, 2, "Promised", _promised);
        Pair(0, 4, "Status", _status);
        Pair(1, 0, "Mileage in", _mileage);
        Pair(1, 2, "Technician", _tech);
        top.Controls.Add(Ui.Button("Change Customer/Vehicle", (_, _) => ChangeCustomer()), 5, 1);

        var text = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Height = 230, Padding = new Padding(8, 0, 8, 0) };
        text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        GroupBox Box(string title, Control c)
        {
            var g = new GroupBox { Text = title, Dock = DockStyle.Fill };
            c.Dock = DockStyle.Fill;
            g.Controls.Add(c);
            return g;
        }
        text.Controls.Add(Box("Customer concern", _complaint), 0, 0);
        text.Controls.Add(Box("Cause / diagnosis", _cause), 1, 0);
        text.Controls.Add(Box("Correction / work performed", _correction), 0, 1);
        text.Controls.Add(Box("Notes", _notes), 1, 1);

        _lines = new LineItemsEditor(app) { Dock = DockStyle.Fill };
        var linesBox = new GroupBox { Text = "Planned labor and parts", Dock = DockStyle.Fill };
        linesBox.Controls.Add(_lines);

        var actions = Ui.ButtonBar(DockStyle.Bottom);
        actions.Controls.AddRange(new Control[]
        {
            Ui.Button("Save", (_, _) => Ui.Try(Save)),
            Ui.Button("Print Preview", (_, _) => PrintDoc(preview: true)),
            Ui.Button("Print", (_, _) => PrintDoc(preview: false)),
            Ui.Button("Create Invoice", (_, _) => CreateInvoice()),
            Ui.Button("Close", (_, _) => Close()),
        });

        Controls.Add(linesBox);
        Controls.Add(text);
        Controls.Add(top);
        Controls.Add(_header);
        Controls.Add(actions);
        LoadTicket();
    }

    private void LoadTicket()
    {
        _opened.Value = _ticket.DateOpened;
        _promised.Checked = _ticket.Promised.HasValue;
        _promised.Value = _ticket.Promised ?? DateTime.Today;
        _status.SelectedItem = _ticket.Status;
        _mileage.Text = _ticket.Mileage?.ToString() ?? "";
        _tech.Text = _ticket.Technician;
        _complaint.Text = _ticket.Complaint;
        _cause.Text = _ticket.Cause;
        _correction.Text = _ticket.Correction;
        _notes.Text = _ticket.Notes;
        _lines.Lines = _ticket.Lines;
        UpdateHeader();
    }

    private void UpdateHeader()
    {
        var label = _ticket.Id == 0 ? "New job ticket" : $"Job ticket #{_ticket.Number}";
        Text = label;
        _header.Text = $"{label}   {_customer.Name} {_customer.Phone}   {_vehicle}";
    }

    private void Save()
    {
        _ticket.DateOpened = _opened.Value.Date;
        _ticket.Promised = _promised.Checked ? _promised.Value.Date : null;
        _ticket.Status = (JobStatus)_status.SelectedItem!;
        _ticket.Mileage = Ui.ParseOptionalInt(_mileage.Text, "Mileage");
        _ticket.Technician = _tech.Text.Trim();
        _ticket.Complaint = _complaint.Text.Trim();
        _ticket.Cause = _cause.Text.Trim();
        _ticket.Correction = _correction.Text.Trim();
        _ticket.Notes = _notes.Text.Trim();
        _ticket.Lines = _lines.Lines;
        if (_ticket.Lines.Any(l => string.IsNullOrWhiteSpace(l.Description)))
            throw new ArgumentException("Every line needs a description.");
        _app.Tickets.Save(_ticket);
        _ticket = _app.Tickets.Get(_ticket.Id)!;
        LoadTicket();
    }

    private void ChangeCustomer()
    {
        using var picker = new CustomerVehiclePickerForm(_app, "Change Customer / Vehicle");
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        _customer = picker.Customer!;
        _vehicle = picker.Vehicle!;
        _ticket.CustomerId = _customer.Id;
        _ticket.VehicleId = _vehicle.Id;
        UpdateHeader();
    }

    private void CreateInvoice()
    {
        if (!Ui.Try(Save))
            return;
        if (_ticket.Status == JobStatus.Invoiced && !Ui.Confirm("This ticket was already invoiced. Create another invoice?"))
            return;
        Invoice? inv = null;
        if (!Ui.Try(() => inv = _app.InvoiceService.CreateFromJobTicket(_ticket.Id)))
            return;
        using var f = new InvoiceEditForm(_app, _app.Invoices.Get(inv!.Id)!);
        f.ShowDialog(this);
        _ticket = _app.Tickets.Get(_ticket.Id)!;
        LoadTicket();
    }

    private void PrintDoc(bool preview)
    {
        if (!Ui.Try(Save))
            return;
        var doc = Documents.JobTicket(_ticket, _customer, _vehicle, _app.LoadSettings());
        if (preview)
            PrintHelper.Preview(this, doc);
        else
            PrintHelper.Print(this, doc);
    }
}
