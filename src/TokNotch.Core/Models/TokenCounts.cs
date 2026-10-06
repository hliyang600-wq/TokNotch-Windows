namespace TokNotch.Core.Models;

/// <summary>Disjoint, already-normalized backend buckets. Reasoning is separate from output.</summary>
public sealed record TokenCounts
{
    public long Input { get; }
    public long Output { get; }
    public long CacheRead { get; }
    public long CacheWrite { get; }
    public long Reasoning { get; }
    public long Total => checked(Input + Output + CacheRead + CacheWrite + Reasoning);

    public TokenCounts(long input, long output, long cacheRead = 0, long cacheWrite = 0, long reasoning = 0)
    {
        if (input < 0 || output < 0 || cacheRead < 0 || cacheWrite < 0 || reasoning < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Token buckets must be nonnegative.");
        Input = input; Output = output; CacheRead = cacheRead; CacheWrite = cacheWrite; Reasoning = reasoning;
        _ = Total;
    }
}
