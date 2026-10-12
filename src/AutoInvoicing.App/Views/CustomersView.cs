using AutoInvoicing.App.Forms;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Views;

internal sealed class CustomersView : UserControl, IRefreshable
{
    private readonly AppServices _app;
    private readonly DataGridView _customers = Ui.Grid();
    private readonly DataGridView _vehicles = Ui.Grid();
    private readonly TextBox _search = Ui.Text();

    public string Title => "Customers & History";

    public CustomersView(AppServices app)
    {
        _app = app;
        _customers.Columns.AddRange(
            Ui.Col("Name", nameof(Customer.Name), 90),
            Ui.Col("Phone", nameof(Customer.Phone), 50),
            Ui.Col("Email", nameof(Customer.Email), 70));
        _vehicles.Columns.AddRange(
            Ui.Col("Vehicle", nameof(Vehicle.Description), 100),
            Ui.Col("Plate", nameof(Vehicle.Plate), 40),
            Ui.Col("VIN", nameof(Vehicle.Vin), 80));
        _customers.SelectionChanged += (_, _) => LoadVehicles();
        _customers.DoubleClick += (_, _) => History(vehicleOnly: false);
        _vehicles.DoubleClick += (_, _) => History(vehicleOnly: true);

        var custBar = Ui.ButtonBar();
        custBar.Controls.AddRange(new Control[]
        {
            Ui.Button("New Customer", (_, _) => EditCustomer(new Customer())),
            Ui.Button("Edit", (_, _) => { if (_customers.Selected<Customer>() is { } c) EditCustomer(c); }),
            Ui.Button("Delete", (_, _) => DeleteCustomer()),
            Ui.Button("Customer History", (_, _) => History(vehicleOnly: false)),
            new Label { Text = "  Search:", AutoSize = true, Margin = new Padding(3, 9, 3, 3) }, _search,
        });
        _search.Width = 200;
        _search.Margin = new Padding(3, 6, 3, 3);
        _search.TextChanged += (_, _) => RefreshData();

        var vehBar = Ui.ButtonBar();
        vehBar.Controls.AddRange(new Control[]
        {
            Ui.Button("Add Vehicle", (_, _) => AddVehicle()),
            Ui.Button("Edit Vehicle", (_, _) => EditVehicle()),
            Ui.Button("Delete Vehicle", (_, _) => DeleteVehicle()),
            Ui.Button("Vehicle History", (_, _) => History(vehicleOnly: true)),
        });

        var split = Ui.Split(0.55f);
        var custBox = new GroupBox { Text = "Customers", Dock = DockStyle.Fill };
        custBox.Controls.Add(_customers);
        custBox.Controls.Add(custBar);
        var vehBox = new GroupBox { Text = "Vehicles", Dock = DockStyle.Fill };
        vehBox.Controls.Add(_vehicles);
        vehBox.Controls.Add(vehBar);
        split.Panel1.Controls.Add(custBox);
        split.Panel2.Controls.Add(vehBox);
        Controls.Add(split);
    }

    public void RefreshData()
    {
        var selected = _customers.Selected<Customer>()?.Id;
        var list = _customers.Bind(_app.Customers.List(_search.Text));
        if (selected != null && list.FirstOrDefault(c => c.Id == selected) is { } c)
            _customers.CurrentCell = _customers.Rows[list.IndexOf(c)].Cells[0];
        LoadVehicles();
    }

    private void LoadVehicles()
    {
        var c = _customers.Selected<Customer>();
        _vehicles.Bind(c == null ? new List<Vehicle>() : _app.Vehicles.ListByCustomer(c.Id));
    }

    private void EditCustomer(Customer c)
    {
        using var f = new CustomerEditForm(_app, c);
        if (f.ShowDialog(this) == DialogResult.OK)
            RefreshData();
    }

    private void DeleteCustomer()
    {
        if (_customers.Selected<Customer>() is { } c && Ui.Confirm($"Delete {c.Name} and their vehicles?") &&
            Ui.Try(() => _app.Customers.Delete(c.Id)))
            RefreshData();
    }

    private void AddVehicle()
    {
        if (_customers.Selected<Customer>() is not { } c)
            return;
        using var f = new VehicleEditForm(_app, new Vehicle { CustomerId = c.Id });
        if (f.ShowDialog(this) == DialogResult.OK)
            LoadVehicles();
    }

    private void EditVehicle()
    {
        if (_vehicles.Selected<Vehicle>() is not { } v)
            return;
        using var f = new VehicleEditForm(_app, v);
        if (f.ShowDialog(this) == DialogResult.OK)
            LoadVehicles();
    }

    private void DeleteVehicle()
    {
        if (_vehicles.Selected<Vehicle>() is { } v && Ui.Confirm($"Delete {v}?") && Ui.Try(() => _app.Vehicles.Delete(v.Id)))
            LoadVehicles();
    }

    private void History(bool vehicleOnly)
    {
        if (_customers.Selected<Customer>() is not { } c)
            return;
        var v = vehicleOnly ? _vehicles.Selected<Vehicle>() : null;
        if (vehicleOnly && v == null)
            return;
        using var f = new HistoryForm(_app, c, v);
        f.ShowDialog(this);
    }
}
