namespace AutoInvoicing.Core.Models;

/// <summary>
/// One line on a job ticket or invoice. Parts lines carry the inventory part id
/// and the unit cost at the time they were added, so reports can show margin.
/// </summary>
public class LineItem
{
    public long Id { get; set; }
    public LineType Type { get; set; }
    public long? PartId { get; set; }
    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public bool Taxable { get; set; }

    public decimal Total => Money.Round(Quantity * UnitPrice);

    public LineItem Clone() => (LineItem)MemberwiseClone();
}
