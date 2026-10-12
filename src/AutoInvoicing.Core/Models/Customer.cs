namespace AutoInvoicing.Core.Models;

public class Customer
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Notes { get; set; } = "";

    public override string ToString() => Name;
}

public class Vehicle
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public int? Year { get; set; }
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public string Vin { get; set; } = "";
    public string Plate { get; set; } = "";
    public string Color { get; set; } = "";
    public string Engine { get; set; } = "";

    public string Description =>
        string.Join(" ", new[] { Year?.ToString() ?? "", Make, Model }.Where(s => s.Length > 0));

    public override string ToString() =>
        Plate.Length > 0 ? $"{Description} ({Plate})" : Description;
}
