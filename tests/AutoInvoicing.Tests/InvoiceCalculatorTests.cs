using AutoInvoicing.Core;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.Tests;

public class InvoiceCalculatorTests
{
    private static LineItem Line(LineType type, decimal qty, decimal price, bool taxable, decimal cost = 0m) =>
        new() { Type = type, Quantity = qty, UnitPrice = price, UnitCost = cost, Taxable = taxable };

    [Fact]
    public void Totals_split_by_line_type()
    {
        var lines = new[]
        {
            Line(LineType.Labor, 1.5m, 120m, taxable: false),
            Line(LineType.Part, 2m, 45.99m, taxable: true, cost: 22.50m),
            Line(LineType.Fee, 1m, 12.50m, taxable: true),
        };

        var t = InvoiceCalculator.Calculate(lines, 6.625m);

        Assert.Equal(180.00m, t.LaborTotal);
        Assert.Equal(91.98m, t.PartsTotal);
        Assert.Equal(12.50m, t.FeesTotal);
        Assert.Equal(284.48m, t.Subtotal);
        Assert.Equal(104.48m, t.TaxableAmount);
        Assert.Equal(6.92m, t.Tax); // 104.48 * 6.625% = 6.9218
        Assert.Equal(291.40m, t.Total);
        Assert.Equal(45.00m, t.PartsCost);
    }

    [Fact]
    public void Labor_is_taxed_only_when_flagged()
    {
        var untaxed = InvoiceCalculator.Calculate([Line(LineType.Labor, 2m, 100m, taxable: false)], 8m);
        var taxed = InvoiceCalculator.Calculate([Line(LineType.Labor, 2m, 100m, taxable: true)], 8m);

        Assert.Equal(0m, untaxed.Tax);
        Assert.Equal(200m, untaxed.Total);
        Assert.Equal(16.00m, taxed.Tax);
        Assert.Equal(216.00m, taxed.Total);
    }

    [Theory]
    [InlineData("10.00", "7.25", "0.73")]   // 0.725 rounds up at the half cent
    [InlineData("10.00", "7.24", "0.72")]
    [InlineData("33.33", "6", "2.00")]      // 1.9998
    [InlineData("0.07", "7", "0.00")]       // 0.0049
    [InlineData("0.10", "5", "0.01")]       // 0.005 rounds away from zero, not to even
    public void Tax_rounds_half_cent_away_from_zero(string taxable, string rate, string expected)
    {
        Assert.Equal(decimal.Parse(expected), InvoiceCalculator.CalculateTax(decimal.Parse(taxable), decimal.Parse(rate)));
    }

    [Fact]
    public void Each_line_is_rounded_to_cents_before_summing()
    {
        // 0.3 qt at $4.99 = 1.497 -> 1.50 each; three lines print as 1.50 and must total 4.50, not 4.49.
        var lines = Enumerable.Range(0, 3).Select(_ => Line(LineType.Part, 0.3m, 4.99m, taxable: false)).ToList();

        var t = InvoiceCalculator.Calculate(lines, 0m);

        Assert.All(lines, l => Assert.Equal(1.50m, l.Total));
        Assert.Equal(4.50m, t.Subtotal);
    }

    [Fact]
    public void Decimal_math_has_no_floating_point_drift()
    {
        var lines = Enumerable.Range(0, 10).Select(_ => Line(LineType.Fee, 1m, 0.10m, taxable: false));

        Assert.Equal(1.00m, InvoiceCalculator.Calculate(lines, 0m).Total);
    }

    [Fact]
    public void Balance_due_subtracts_payments()
    {
        var t = InvoiceCalculator.Calculate([Line(LineType.Part, 1m, 100m, taxable: true)], 6m, amountPaid: 50m);

        Assert.Equal(106.00m, t.Total);
        Assert.Equal(56.00m, t.BalanceDue);
    }

    [Fact]
    public void Negative_line_such_as_core_credit_reduces_total_and_tax()
    {
        var lines = new[]
        {
            Line(LineType.Part, 1m, 150m, taxable: true),
            Line(LineType.Part, 1m, -25m, taxable: true),
        };

        var t = InvoiceCalculator.Calculate(lines, 10m);

        Assert.Equal(125m, t.Subtotal);
        Assert.Equal(12.50m, t.Tax);
        Assert.Equal(137.50m, t.Total);
    }

    [Fact]
    public void Empty_invoice_is_zero()
    {
        var t = InvoiceCalculator.Calculate([], 7m);

        Assert.Equal(0m, t.Total);
        Assert.Equal(0m, t.Tax);
    }

    [Fact]
    public void Negative_tax_rate_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InvoiceCalculator.Calculate([], -1m));
    }

    [Fact]
    public void Default_taxable_follows_shop_settings()
    {
        var settings = new ShopSettings { TaxLabor = false, TaxFees = true };

        Assert.False(InvoiceCalculator.DefaultTaxable(LineType.Labor, settings));
        Assert.True(InvoiceCalculator.DefaultTaxable(LineType.Fee, settings));
        Assert.True(InvoiceCalculator.DefaultTaxable(LineType.Part, settings));
        Assert.False(InvoiceCalculator.DefaultTaxable(LineType.Part, settings, partTaxable: false));
    }
}
