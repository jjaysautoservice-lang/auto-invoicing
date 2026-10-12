namespace AutoInvoicing.Core.Models;

public enum LineType
{
    Labor = 0,
    Part = 1,
    Fee = 2,
}

public enum InvoiceStatus
{
    Draft = 0,
    Finalized = 1,
    Void = 2,
}

public enum JobStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Invoiced = 3,
    Cancelled = 4,
}
