namespace AutoInvoicing.Core.Models;

public class ShopSettings
{
    public string ShopName { get; set; } = "My Auto Repair";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string TaxId { get; set; } = "";
    public decimal TaxRatePercent { get; set; }
    public bool TaxLabor { get; set; }
    public bool TaxFees { get; set; } = true;
    public decimal LaborRate { get; set; } = 100m;
    public string InvoiceFooter { get; set; } =
        "Thank you for your business! All parts and labor warranted per shop policy.";
}
