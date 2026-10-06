namespace TokNotch.Core.Models;

/// <summary>
/// Single place for "how full is the ring". Amount rings share one configurable full-circle baseline per currency;
/// quota rings always stay percentages and are never converted into money.
/// </summary>
public static class RingMath
{
    public const decimal DefaultAmountBaseline = 50m;

    /// <summary>balance / baseline, clamped to a full circle. Null balance (unknown) or a missing/zero baseline stays unknown.</summary>
    public static double? AmountFraction(decimal? balance, decimal? baseline)
    {
        if (balance is not decimal amount || baseline is not decimal limit || limit <= 0m) return null;
        var ratio = amount / limit;
        return ratio <= 0m ? 0d : ratio >= 1m ? 1d : (double)ratio;
    }

    /// <summary>Official "used percent" is converted into the remaining percent the rings show. Out-of-range values clamp, never wrap.</summary>
    public static double? RemainingFraction(double? usedPercent)
    {
        if (usedPercent is not double used || !double.IsFinite(used)) return null;
        var remaining = (100d - used) / 100d;
        return remaining <= 0d ? 0d : remaining >= 1d ? 1d : remaining;
    }

    public static string Percent(double? fraction) => fraction is double value && double.IsFinite(value)
        ? $"{Math.Round(Math.Clamp(value, 0, 1) * 100d, MidpointRounding.AwayFromZero):0}%"
        : "—";

    /// <summary>Money text keeps the source currency instead of forcing yuan onto a USD account. Matches the numeric rows exactly.</summary>
    public static string Money(decimal? amount, string currency) => amount is not decimal value
        ? "—"
        : value.ToString(
            currency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? "$0.00" : "¥0.00",
            System.Globalization.CultureInfo.InvariantCulture);
}
