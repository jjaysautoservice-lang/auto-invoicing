namespace AutoInvoicing.Core.Models;

public class Invoice
{
    public long Id { get; set; }
    public int Number { get; set; }
    public long CustomerId { get; set; }
    public long VehicleId { get; set; }
    public long? JobTicketId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public int? Mileage { get; set; }
    public decimal TaxRatePercent { get; set; }
    public string Notes { get; set; } = "";
    public string VoidReason { get; set; } = "";
    public List<LineItem> Lines { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();

    public decimal AmountPaid => Payments.Sum(p => p.Amount);

    public InvoiceTotals Totals => InvoiceCalculator.Calculate(Lines, TaxRatePercent, AmountPaid);
}

public class Payment
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";
    public string Reference { get; set; } = "";
}

/// <summary>Invoice header with customer/vehicle names, used in lists and history.</summary>
public class InvoiceSummary
{
    public long Id { get; set; }
    public int Number { get; set; }
    public DateTime InvoiceDate { get; set; }
    public InvoiceStatus Status { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public long VehicleId { get; set; }
    public string VehicleDescription { get; set; } = "";
    public int? Mileage { get; set; }
    public decimal Total { get; set; }
    public decimal BalanceDue { get; set; }
    public string WorkSummary { get; set; } = "";
}

public class JobTicketSummary
{
    public long Id { get; set; }
    public int Number { get; set; }
    public DateTime DateOpened { get; set; }
    public JobStatus Status { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public long VehicleId { get; set; }
    public string VehicleDescription { get; set; } = "";
    public string Technician { get; set; } = "";
    public string Complaint { get; set; } = "";
}
