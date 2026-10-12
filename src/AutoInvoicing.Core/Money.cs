using System.Globalization;

namespace AutoInvoicing.Core;

public static class Money
{
    /// <summary>Rounds to cents, halves away from zero (how a register rounds).</summary>
    public static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static string Format(decimal amount) =>
        amount.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
}
