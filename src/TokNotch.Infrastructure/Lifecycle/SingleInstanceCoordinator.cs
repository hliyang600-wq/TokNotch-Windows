using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace TokNotch.Infrastructure.Lifecycle;

public enum LaunchRequest { Show, Settings, Connections, Appearance, MimoLogin }

/// <summary>One application per Windows user/session, shared by every installation path.</summary>
public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly string identity;
    private readonly Mutex mutex;
    private readonly List<EventWaitHandle> signals = new();
    private readonly List<RegisteredWaitHandle> registrations = new();
    private bool disposed;
    public bool IsPrimary { get; }

    // The primary must be created and disposed on the same thread (the WPF dispatcher).
    public SingleInstanceCoordinator(Action<LaunchRequest> received, string? testIdentity = null)
    {
        var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var scope = $"{user}:{Process.GetCurrentProcess().SessionId}:{testIdentity ?? "production"}";
        identity = "Local\\TokNotchWindows.Instance.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
        mutex = new Mutex(false, identity);
        try { IsPrimary = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
        if (!IsPrimary) return;
        try
        {
            foreach (var request in Enum.GetValues<LaunchRequest>())
            {
                var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName(request));
                signals.Add(signal);
                registrations.Add(ThreadPool.RegisterWaitForSingleObject(signal, (_, _) =>
                {
                    if (!Volatile.Read(ref disposed)) received(request);
                }, null, Timeout.Infinite, false));
            }
        }
        catch { Dispose(); throw; }
    }

    public static LaunchRequest ParseRequest(IEnumerable<string> arguments)
    {
        var args = arguments.ToHashSet(StringComparer.Ordinal);
        if (args.Contains("--mimo-login")) return LaunchRequest.MimoLogin;
        if (args.Contains("--appearance")) return LaunchRequest.Appearance;
        if (args.Contains("--connections")) return LaunchRequest.Connections;
        return args.Contains("--settings") ? LaunchRequest.Settings : LaunchRequest.Show;
    }

    public async Task<bool> NotifyAsync(LaunchRequest request)
    {
        if (IsPrimary || !Enum.IsDefined(request)) return false;
        var deadline = Stopwatch.StartNew();
        // An existing process may still be creating its notification handles.
        while (deadline.Elapsed < TimeSpan.FromSeconds(2))
        {
            if (EventWaitHandle.TryOpenExisting(SignalName(request), out var signal))
            {
                using (signal) return signal.Set();
            }
            await Task.Delay(25).ConfigureAwait(false);
        }
        return false; // Never create another window when the owner is busy starting.
    }

    private string SignalName(LaunchRequest request) => identity + "." + request;

    public void Dispose()
    {
        if (Volatile.Read(ref disposed)) return;
        Volatile.Write(ref disposed, true);
        foreach (var registration in registrations) registration.Unregister(null);
        foreach (var signal in signals) signal.Dispose();
        if (IsPrimary) mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
