namespace AutoInvoicing.Core.Models;

public class Part
{
    public long Id { get; set; }
    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Location { get; set; } = "";
    public decimal Cost { get; set; }
    public decimal Price { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool Taxable { get; set; } = true;
    public bool Active { get; set; } = true;

    public bool IsLowStock => QuantityOnHand <= ReorderLevel;

    public override string ToString() => $"{PartNumber} - {Description}";
}

public class InventoryTransaction
{
    public long Id { get; set; }
    public long PartId { get; set; }
    public DateTime Date { get; set; }
    public decimal QuantityChange { get; set; }
    public string Reason { get; set; } = "";
    public long? InvoiceId { get; set; }
}
