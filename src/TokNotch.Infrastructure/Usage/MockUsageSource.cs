using TokNotch.Core.Abstractions;
using TokNotch.Core.Models;
namespace TokNotch.Infrastructure.Usage;

public enum DemoScenario { Normal, NoUsage, NotDetected, RefreshFailed, PriceUnavailable }

/// <summary>Synthetic, fixed fixture data. Never reads local CLI directories.</summary>
public sealed class MockUsageSource : IUsageSnapshotSource
{
    public DemoScenario Scenario { get; set; }
    public Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stamp = DateTimeOffset.Parse("2026-10-02T12:00:00+08:00", System.Globalization.CultureInfo.InvariantCulture);
        var vendors = new[]
        {
            Build(Provider.Claude, "claude", "Claude", "Claude Sonnet", 18.20m, 246.50m, 12_400_000, 100m, 480m, .72),
            Build(Provider.OpenAI, "codex", "Codex", "OpenAI Codex", 6.40m, 62m, 3_100_000, 20m, 62m, .46),
            Build(Provider.Gemini, "gemini", "Gemini", "Gemini Pro", 2.15m, 11.99m, 980_000, 20m, 11.99m, .23)
        };
        return Task.FromResult(new UsageSnapshot(stamp, stamp, "Asia/Shanghai", true,
            Scenario == DemoScenario.RefreshFailed ? RefreshState.Failed : RefreshState.Ready,
            vendors, vendors.Select(v => new ModelUsage(v.Model, v.Provider, v.Month.Tokens, v.Month.EstimatedCost)),
            Scenario == DemoScenario.RefreshFailed ? "Unable to refresh · showing previous demo snapshot" : null));
    }
    private ProviderUsageSnapshot Build(Provider provider, string client, string title, string model,
        decimal today, decimal month, long tokens, decimal paid, decimal earned, double fraction)
    {
        var missing = Scenario == DemoScenario.NotDetected;
        var empty = Scenario is DemoScenario.NoUsage or DemoScenario.NotDetected;
        var unpriced = Scenario == DemoScenario.PriceUnavailable;
        var costState = unpriced ? CostStatus.Unavailable : CostStatus.Complete;
        PeriodUsage Period(long amount, decimal cost) => new(new TokenCounts(empty ? 0 : amount * 7 / 10,
            empty ? 0 : amount / 10, empty ? 0 : amount - amount * 7 / 10 - amount / 10),
            unpriced || missing ? null : empty ? 0m : cost, costState);
        return new(provider, client, title, model,
            missing ? DetectionState.NotDetected : empty ? DetectionState.DetectedNoUsage : DetectionState.Ready,
            Period(tokens / 12, today), Period(tokens / 2, month / 2), Period(tokens, month), Period(tokens * 4, month * 4),
            new(paid, unpriced || missing ? null : empty ? 0m : earned, new(2026, 9, 3), new(2026, 10, 3)),
            new($"demo.{client}", empty || unpriced ? null : fraction, "Today / recent peak · demo"));
    }
}
