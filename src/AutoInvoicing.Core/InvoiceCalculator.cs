using AutoInvoicing.Core.Models;

namespace AutoInvoicing.Core;

public sealed record InvoiceTotals(
    decimal LaborTotal,
    decimal PartsTotal,
    decimal FeesTotal,
    decimal Subtotal,
    decimal TaxableAmount,
    decimal Tax,
    decimal Total,
    decimal AmountPaid,
    decimal BalanceDue,
    decimal PartsCost);

public static class InvoiceCalculator
{
    /// <summary>
    /// Each line is rounded to cents first, then summed. Tax is computed once on the
    /// sum of taxable lines and rounded to cents, so the printed lines always add up.
    /// </summary>
    public static InvoiceTotals Calculate(IEnumerable<LineItem> lines, decimal taxRatePercent, decimal amountPaid = 0m)
    {
        if (taxRatePercent < 0m)
            throw new ArgumentOutOfRangeException(nameof(taxRatePercent), "Tax rate cannot be negative.");

        decimal labor = 0m, parts = 0m, fees = 0m, taxable = 0m, partsCost = 0m;
        foreach (var line in lines)
        {
            var total = line.Total;
            switch (line.Type)
            {
                case LineType.Labor: labor += total; break;
                case LineType.Part:
                    parts += total;
                    partsCost += Money.Round(line.Quantity * line.UnitCost);
                    break;
                default: fees += total; break;
            }
            if (line.Taxable)
                taxable += total;
        }

        var subtotal = labor + parts + fees;
        var tax = CalculateTax(taxable, taxRatePercent);
        var grandTotal = subtotal + tax;
        return new InvoiceTotals(labor, parts, fees, subtotal, taxable, tax, grandTotal,
            amountPaid, grandTotal - amountPaid, partsCost);
    }

    public static decimal CalculateTax(decimal taxableAmount, decimal taxRatePercent) =>
        Money.Round(taxableAmount * taxRatePercent / 100m);

    /// <summary>Default taxable flag for a new line based on shop settings.</summary>
    public static bool DefaultTaxable(LineType type, ShopSettings settings, bool partTaxable = true) => type switch
    {
        LineType.Labor => settings.TaxLabor,
        LineType.Part => partTaxable,
        _ => settings.TaxFees,
    };
}
