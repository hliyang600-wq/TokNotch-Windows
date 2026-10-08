namespace TokNotch.Core.Models;

/// <summary>Display rows reuse the measures already supplied to rings, without sharing their selection.</summary>
public static class DisplayRows
{
    public static IReadOnlyList<RingContent> Available(Provider provider) => RingChoices.Available(provider)
        .Concat(provider==Provider.DeepSeek?new[]{RingContent.CashBalance,RingContent.GiftBalance}:Array.Empty<RingContent>())
        .Concat(provider==Provider.Qwen?new[]{RingContent.TodayTokens,RingContent.QuotaResetTime}:Array.Empty<RingContent>())
        .Append(RingContent.None).ToArray();
    public static string Name(RingContent metric)=>metric==RingContent.None?"不显示这一行":RingChoices.Name(metric);

    public static RingContent FromLegacy(UsageMetric metric) => metric switch
    {
        UsageMetric.Today => RingContent.TodayTokens,
        UsageMetric.Month => RingContent.MonthTokens,
        _ => RingContent.AllTimeTokens
    };

    public static IReadOnlyList<RingContent> Default(Provider provider, IReadOnlyList<UsageMetric> legacy)
    {
        if(provider==Provider.Qwen)return new[]{RingContent.TodayTokens,RingContent.QuotaResetTime,RingContent.None};
        if(provider == Provider.OpenAI || !ProviderCatalog.Available.Any(p=>p.Id==provider))
            return legacy.Select(FromLegacy).ToArray();
        return legacy.Where(m=>m!=UsageMetric.AllTime).Select(m=>provider==Provider.Kimi
            ? m==UsageMetric.Today ? RingContent.CashBalance : RingContent.GiftBalance
            : FromLegacy(m)).Append(RingContent.Balance).ToArray();
    }

    public static void Validate(Provider provider, IReadOnlyList<RingContent> rows)
    {
        if(rows is null || rows.Count!=3 || rows.Where(m=>m!=RingContent.None).Distinct().Count()!=rows.Count(m=>m!=RingContent.None) || rows.Any(m=>!Available(provider).Contains(m)))
            throw new ArgumentException($"{ProviderCatalog.Name(provider)} 请选择可用指标，显示的指标不可重复。");
    }
}
