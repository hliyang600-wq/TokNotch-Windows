namespace TokNotch.Core.Models;

public sealed record PeriodUsage(TokenCounts Tokens, decimal? EstimatedCost, CostStatus CostStatus);
public sealed record ModelUsage(string Model, Provider Provider, TokenCounts Tokens, decimal? EstimatedCost);
public sealed record SubscriptionUsage(decimal PriceUsd, decimal? EarnedUsd, DateOnly Start, DateOnly End)
{
    public decimal? PaybackRatio => PriceUsd > 0 && EarnedUsd.HasValue ? EarnedUsd / PriceUsd : null;
}
/// <summary>A ring is a "how much is left" gauge. Fraction is clamped to 0..1 by the renderer; null means unknown and is never drawn as zero.</summary>
public sealed record RingSnapshot(string StableId, double? Fraction, string BaselineLabel)
{
    /// <summary>Short window label such as "5h" or "周"; null for a single-ring source.</summary>
    public string? WindowLabel { get; init; }
    /// <summary>When the underlying sample was written (local log snapshots are not live data).</summary>
    public DateTimeOffset? RecordedAt { get; init; }
    /// <summary>Official window reset moment, when the source publishes one.</summary>
    public DateTimeOffset? ResetsAt { get; init; }
    /// <summary>True when the reset moment has already passed, so the recorded value must not be read as current.</summary>
    public bool ResetElapsed { get; init; }
    public bool IsStale => ResetElapsed;
}
public sealed record ProviderUsageSnapshot(
    Provider Provider, string ClientId, string Title, string Model, DetectionState Detection,
    PeriodUsage Today, PeriodUsage Week, PeriodUsage Month, PeriodUsage AllTime,
    SubscriptionUsage? Subscription, RingSnapshot Ring) { public string? SourceStatus { get; init; } public bool RefreshFailed { get; init; } public decimal? Balance { get; init; } public decimal? Cash { get; init; } public decimal? Voucher { get; init; } public string Currency { get; init; } = "CNY"; public string? BalanceStatus { get; init; } public string? BalanceSource { get; init; } public bool TokenUsageAvailable { get; init; } = true; public Provider? TokenUsageParent {get;init;} public RingSnapshot? InnerRing { get; init; } }

/// <summary>The sole UI usage-data input. Collections are frozen by the constructor.</summary>
public sealed class UsageSnapshot
{
    public int SchemaVersion => 1;
    public DateTimeOffset GeneratedAt { get; }
    public DateTimeOffset LastSuccessAt { get; }
    public string BucketTimeZone { get; }
    public bool IsDemo { get; }
    public RefreshState RefreshState { get; }
    public string? StatusMessage { get; }
    public IReadOnlyList<ProviderUsageSnapshot> Vendors { get; }
    public IReadOnlyList<ModelUsage> Models { get; }
    public long TodayTokens => SumTokens(p => p.Today);
    public long WeekTokens => SumTokens(p => p.Week);
    public long MonthTokens => SumTokens(p => p.Month);
    public long TotalTokens => SumTokens(p => p.AllTime);
    public decimal? TodayCost => SumCost(p => p.Today);
    public decimal? MonthCost => SumCost(p => p.Month);

    public UsageSnapshot(DateTimeOffset generatedAt, DateTimeOffset lastSuccessAt, string bucketTimeZone,
        bool isDemo, RefreshState refreshState, IEnumerable<ProviderUsageSnapshot> vendors,
        IEnumerable<ModelUsage> models, string? statusMessage = null)
    {
        GeneratedAt = generatedAt; LastSuccessAt = lastSuccessAt; BucketTimeZone = bucketTimeZone;
        IsDemo = isDemo; RefreshState = refreshState; StatusMessage = statusMessage;
        Vendors = Array.AsReadOnly(vendors.ToArray()); Models = Array.AsReadOnly(models.ToArray());
    }

    private long SumTokens(Func<ProviderUsageSnapshot, PeriodUsage> select) =>
        Vendors.Where(p=>p.TokenUsageParent is not {} parent || !Vendors.Any(v=>v.Provider==parent)).Aggregate(0L, (sum, p) => checked(sum + select(p).Tokens.Total));
    private decimal? SumCost(Func<ProviderUsageSnapshot, PeriodUsage> select)
    {
        if (Vendors.Count == 0) return null;
        decimal total = 0;
        foreach (var provider in Vendors)
        {
            var period = select(provider);
            if (period.CostStatus != CostStatus.Complete || !period.EstimatedCost.HasValue) return null;
            total += period.EstimatedCost.Value;
        }
        return total;
    }
}
