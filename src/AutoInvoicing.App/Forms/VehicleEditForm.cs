using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

internal sealed class VehicleEditForm : Form
{
    public Vehicle Vehicle { get; }

    public VehicleEditForm(AppServices app, Vehicle vehicle)
    {
        Vehicle = vehicle;
        Ui.SetupDialog(this, vehicle.Id == 0 ? "New Vehicle" : "Edit Vehicle", 460, 340);
        var t = Ui.FieldTable();
        var year = t.AddField("Year", Ui.Text(vehicle.Year?.ToString() ?? ""));
        var make = t.AddField("Make", Ui.Text(vehicle.Make));
        var model = t.AddField("Model", Ui.Text(vehicle.Model));
        var engine = t.AddField("Engine", Ui.Text(vehicle.Engine));
        var color = t.AddField("Color", Ui.Text(vehicle.Color));
        var vin = t.AddField("VIN", Ui.Text(vehicle.Vin));
        var plate = t.AddField("Plate", Ui.Text(vehicle.Plate));
        vin.CharacterCasing = CharacterCasing.Upper;
        plate.CharacterCasing = CharacterCasing.Upper;
        Controls.Add(t);
        Controls.Add(Ui.DialogButtons(this, () =>
        {
            var y = Ui.ParseOptionalInt(year.Text, "Year");
            if (y is < 1900 or > 2100)
                throw new ArgumentException("Year looks wrong.");
            var v = vin.Text.Trim();
            if (v.Length > 0 && v.Length != 17 && !Ui.Confirm($"VIN is {v.Length} characters (most are 17). Save anyway?"))
                return false;
            vehicle.Year = y;
            vehicle.Make = make.Text.Trim();
            vehicle.Model = model.Text.Trim();
            vehicle.Engine = engine.Text.Trim();
            vehicle.Color = color.Text.Trim();
            vehicle.Vin = v;
            vehicle.Plate = plate.Text.Trim();
            if (vehicle.Make.Length == 0 && vehicle.Model.Length == 0)
                throw new ArgumentException("Enter at least the make or model.");
            app.Vehicles.Save(vehicle);
            return true;
        }));
    }
}
