namespace AutoInvoicing.Core.Services;

public enum PeriodKind
{
    Daily,
    Weekly,
    Monthly,
}

/// <summary>An inclusive date range. Weeks run Monday through Sunday.</summary>
public sealed record ReportPeriod(PeriodKind Kind, DateTime Start, DateTime End)
{
    public static ReportPeriod For(PeriodKind kind, DateTime anyDay)
    {
        var d = anyDay.Date;
        switch (kind)
        {
            case PeriodKind.Daily:
                return new ReportPeriod(kind, d, d);
            case PeriodKind.Weekly:
                var offset = ((int)d.DayOfWeek + 6) % 7; // Monday = 0
                var monday = d.AddDays(-offset);
                return new ReportPeriod(kind, monday, monday.AddDays(6));
            default:
                var first = new DateTime(d.Year, d.Month, 1);
                return new ReportPeriod(kind, first, first.AddMonths(1).AddDays(-1));
        }
    }

    public string Title => Kind switch
    {
        PeriodKind.Daily => $"Daily Report: {Start:dddd, MMMM d, yyyy}",
        PeriodKind.Weekly => $"Weekly Report: {Start:MMM d} to {End:MMM d, yyyy}",
        _ => $"Monthly Report: {Start:MMMM yyyy}",
    };
}
