using System.Diagnostics;
using System.Windows.Media;
namespace TokNotch.UI.Animations;

/// <summary>One rendering subscription for all active animations. UI-thread only.</summary>
public sealed class AnimationClock
{
    public static AnimationClock Current { get; } = new();
    private readonly HashSet<Action<double>> _callbacks = [];
    private readonly Stopwatch _stopwatch = new();
    private double _previous;
    private bool _rendering;
    private Action<double>[]? _snapshot;
    internal bool IsRendering => _rendering;
    internal event Action? FrameCompleted;
    public int ActiveCount => _callbacks.Count;
    public long FrameCount { get; private set; }
    public double MaximumFrameIntervalMs { get; private set; }
    public IDisposable Subscribe(Action<double> callback)
    {
        if (!_callbacks.Add(callback)) throw new InvalidOperationException("Animation already subscribed.");
        _snapshot = null;
        if (_callbacks.Count == 1) { _previous = 0; _stopwatch.Restart(); CompositionTarget.Rendering += Render; }
        return new Subscription(this, callback);
    }
    private void Render(object? sender, EventArgs args)
    {
        if (_rendering) return;
        _rendering = true;
        try
        {
            var now = _stopwatch.Elapsed.TotalSeconds;
            var elapsed = now - _previous; _previous = now;
            if (elapsed <= 0) return;
            FrameCount++; MaximumFrameIntervalMs = Math.Max(MaximumFrameIntervalMs, elapsed * 1000);
            var callbacks = _snapshot ??= _callbacks.ToArray();
            foreach (var callback in callbacks) if (_callbacks.Contains(callback)) callback(elapsed);
        }
        finally { _rendering = false; FrameCompleted?.Invoke(); }
    }
    private void Remove(Action<double> callback)
    {
        _callbacks.Remove(callback);
        _snapshot = null;
        if (_callbacks.Count == 0) { CompositionTarget.Rendering -= Render; _stopwatch.Stop(); }
    }
    private sealed class Subscription(AnimationClock owner, Action<double> callback) : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (_disposed) return; _disposed = true; owner.Remove(callback); }
    }
}
