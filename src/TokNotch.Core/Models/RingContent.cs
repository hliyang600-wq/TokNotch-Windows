namespace TokNotch.Core.Models;

public enum RingContent
{
    None,
    FiveHourRemaining,
    WeeklyRemaining,
    Balance,
    CashBalance,
    GiftBalance,
    TodayTokens,
    MonthTokens,
    AllTimeTokens,
    TodayOfMonth
}

public sealed record RingChoice(RingContent Outer, RingContent Inner, long TokenBaseline = 100_000_000);

public static class RingChoices
{
    public static RingChoice Default(Provider provider) => provider == Provider.OpenAI
        ? new(RingContent.FiveHourRemaining, RingContent.WeeklyRemaining)
        : new(RingContent.Balance, RingContent.None);

    public static IReadOnlyList<RingContent> Available(Provider provider)
    {
        var tokens = new[]{RingContent.TodayTokens,RingContent.MonthTokens,RingContent.AllTimeTokens,RingContent.TodayOfMonth};
        return provider switch
        {
            Provider.OpenAI => new[]{RingContent.FiveHourRemaining,RingContent.WeeklyRemaining}.Concat(tokens).ToArray(),
            Provider.Kimi => new[]{RingContent.Balance,RingContent.CashBalance,RingContent.GiftBalance},
            Provider.Mimo => new[]{RingContent.Balance,RingContent.CashBalance,RingContent.GiftBalance}.Concat(tokens).ToArray(),
            Provider.Dsh or Provider.DeepSeek => new[]{RingContent.Balance}.Concat(tokens).ToArray(),
            _ => Array.Empty<RingContent>()
        };
    }

    public static string Name(RingContent content) => content switch
    {
        RingContent.None => "不显示内圈",
        RingContent.FiveHourRemaining => "5 小时剩余额度",
        RingContent.WeeklyRemaining => "一周剩余额度",
        RingContent.Balance => "可用余额",
        RingContent.CashBalance => "现金余额",
        RingContent.GiftBalance => "赠金余额",
        RingContent.TodayTokens => "今日 Token",
        RingContent.MonthTokens => "本月 Token",
        RingContent.AllTimeTokens => "累计 Token",
        RingContent.TodayOfMonth => "今日占本月",
        _ => "未知指标"
    };

    public static void Validate(Provider provider, RingChoice choice)
    {
        var allowed=Available(provider);
        if (!allowed.Contains(choice.Outer) || choice.Inner != RingContent.None && !allowed.Contains(choice.Inner))
            throw new ArgumentException($"{ProviderCatalog.Name(provider)} 的圆环指标不可用。");
        if (choice.Outer == choice.Inner) throw new ArgumentException("外圈和内圈请选择不同指标。");
        if (choice.TokenBaseline <= 0 || choice.TokenBaseline > 10_000_000_000_000)
            throw new ArgumentException("Token 满圈值需在 1 到 10 万亿之间。");
    }
}
