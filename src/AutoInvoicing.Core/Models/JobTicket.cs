namespace AutoInvoicing.Core.Models;

public class JobTicket
{
    public long Id { get; set; }
    public int Number { get; set; }
    public long CustomerId { get; set; }
    public long VehicleId { get; set; }
    public DateTime DateOpened { get; set; } = DateTime.Today;
    public DateTime? Promised { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Open;
    public int? Mileage { get; set; }
    public string Technician { get; set; } = "";
    public string Complaint { get; set; } = "";
    public string Cause { get; set; } = "";
    public string Correction { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<LineItem> Lines { get; set; } = new();
}
