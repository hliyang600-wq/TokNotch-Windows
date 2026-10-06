using TokNotch.Core.Models;
namespace TokNotch.Core.Abstractions;

public interface IUsageSnapshotSource
{
    Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default);
}

public interface IDeferredScheduler
{
    IDisposable Schedule(TimeSpan delay, Action action);
}
