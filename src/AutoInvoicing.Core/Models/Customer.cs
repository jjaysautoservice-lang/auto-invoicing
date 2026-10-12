namespace AutoInvoicing.Core.Models;

public class Customer
{
    public long Id { get; set; }

    /// <summary>Display name ("First Last", or the company). Built from the name parts when saved.</summary>
    public string Name { get; set; } = "";

    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";

    /// <summary>Main phone number shown in lists and on printouts: the first of <see cref="Phones"/>.</summary>
    public string Phone { get; set; } = "";

    public List<CustomerPhone> Phones { get; set; } = new();
    public string Email { get; set; } = "";

    /// <summary>Full mailing address as printed. Built from the address parts when saved.</summary>
    public string Address { get; set; } = "";

    public string Address1 { get; set; } = "";
    public string Address2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
    public string Notes { get; set; } = "";

    /// <summary>
    /// Trims every field and rebuilds Name, Phone and Address from the separate parts.
    /// Customers saved before the parts existed only have Name/Phone/Address; those are
    /// moved into the parts first so nothing is lost.
    /// </summary>
    public void Normalize()
    {
        LastName = LastName.Trim();
        FirstName = FirstName.Trim();
        if (LastName.Length == 0 && FirstName.Length == 0)
            LastName = Name.Trim();
        Name = FirstName.Length > 0 && LastName.Length > 0 ? $"{FirstName} {LastName}" : LastName + FirstName;

        foreach (var p in Phones)
        {
            p.Type = p.Type.Trim();
            p.Number = p.Number.Trim();
            p.Note = p.Note.Trim();
        }
        Phones.RemoveAll(p => p.Number.Length == 0);
        if (Phones.Count == 0 && Phone.Trim().Length > 0)
            Phones.Add(new CustomerPhone { Number = Phone.Trim() });
        Phone = Phones.FirstOrDefault()?.Number ?? "";

        Address1 = Address1.Trim();
        Address2 = Address2.Trim();
        City = City.Trim();
        State = State.Trim().ToUpperInvariant();
        Zip = Zip.Trim();
        if (Address1.Length + Address2.Length + City.Length + State.Length + Zip.Length == 0)
        {
            var lines = Address.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Address1 = lines.FirstOrDefault() ?? "";
            Address2 = string.Join(", ", lines.Skip(1));
        }
        var cityLine = string.Join(" ", new[] { string.Join(", ", new[] { City, State }.Where(s => s.Length > 0)), Zip }
            .Where(s => s.Length > 0));
        Address = string.Join(Environment.NewLine, new[] { Address1, Address2, cityLine }.Where(s => s.Length > 0));
        Email = Email.Trim();
        Notes = Notes.Trim();
    }

    public override string ToString() => Name;
}

public class CustomerPhone
{
    public static readonly string[] Types = { "Cell", "Home", "Work", "Fax", "Other" };

    public string Type { get; set; } = "";
    public string Number { get; set; } = "";
    public string Note { get; set; } = "";
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
