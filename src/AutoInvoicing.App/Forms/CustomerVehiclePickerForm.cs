using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

/// <summary>Pick (or create) the customer and vehicle for a new job ticket or invoice.</summary>
internal sealed class CustomerVehiclePickerForm : Form
{
    private readonly AppServices _app;
    private readonly DataGridView _customers = Ui.Grid();
    private readonly DataGridView _vehicles = Ui.Grid();
    private readonly TextBox _search = Ui.Text();

    public Customer? Customer { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public CustomerVehiclePickerForm(AppServices app, string title)
    {
        _app = app;
        Ui.SetupDialog(this, title, 900, 520);

        _customers.Columns.AddRange(
            Ui.Col("Name", nameof(Customer.Name), 120),
            Ui.Col("Phone", nameof(Customer.Phone), 70));
        _vehicles.Columns.AddRange(
            Ui.Col("Vehicle", nameof(Vehicle.Description), 120),
            Ui.Col("Plate", nameof(Vehicle.Plate), 50),
            Ui.Col("VIN", nameof(Vehicle.Vin), 90));
        _customers.SelectionChanged += (_, _) => LoadVehicles();
        _vehicles.DoubleClick += (_, _) => { if (Ui.Try(() => Accept())) { DialogResult = DialogResult.OK; Close(); } };

        var top = Ui.FieldTable();
        top.Dock = DockStyle.Top;
        top.AddField("Search name, phone, plate or VIN", _search);
        _search.TextChanged += (_, _) => LoadCustomers();

        var split = Ui.Split(0.5f);
        var custBar = Ui.ButtonBar(DockStyle.Bottom);
        custBar.Controls.Add(Ui.Button("New Customer", (_, _) => NewCustomer()));
        split.Panel1.Controls.Add(_customers);
        split.Panel1.Controls.Add(custBar);
        var vehBar = Ui.ButtonBar(DockStyle.Bottom);
        vehBar.Controls.Add(Ui.Button("New Vehicle", (_, _) => NewVehicle()));
        split.Panel2.Controls.Add(_vehicles);
        split.Panel2.Controls.Add(vehBar);

        Controls.Add(split);
        Controls.Add(top);
        Controls.Add(Ui.DialogButtons(this, Accept, "Select"));
        LoadCustomers();
        Shown += (_, _) => _search.Focus();
    }

    private void LoadCustomers() => _customers.Bind(_app.Customers.List(_search.Text));

    private void LoadVehicles()
    {
        var c = _customers.Selected<Customer>();
        _vehicles.Bind(c == null ? new List<Vehicle>() : _app.Vehicles.ListByCustomer(c.Id));
    }

    private void NewCustomer()
    {
        using var f = new CustomerEditForm(_app, new Customer());
        if (f.ShowDialog(this) != DialogResult.OK)
            return;
        _search.Text = f.Customer.Name;
        LoadCustomers();
        NewVehicle();
    }

    private void NewVehicle()
    {
        var c = _customers.Selected<Customer>();
        if (c == null)
            return;
        using var f = new VehicleEditForm(_app, new Vehicle { CustomerId = c.Id });
        if (f.ShowDialog(this) == DialogResult.OK)
            LoadVehicles();
    }

    private bool Accept()
    {
        Customer = _customers.Selected<Customer>() ?? throw new ArgumentException("Pick a customer.");
        Vehicle = _vehicles.Selected<Vehicle>() ?? throw new ArgumentException("Pick a vehicle, or add one with New Vehicle.");
        return true;
    }
}
