namespace TokNotch.UI.Animations;

/// <summary>Finite, retargetable presentation tween. Unknown values clear immediately.</summary>
public sealed class AnimatedScalar(Action<double?> present) : IDisposable
{
    private IDisposable? _subscription;
    private double _from, _target, _elapsed, _duration;
    private bool _disposed;
    public double? Value { get; private set; }
    public bool IsRunning => _subscription is not null;
    public void Set(double? target, bool animate)
    {
        if (_disposed) return;
        if (target is double number && !double.IsFinite(number)) target = null;
        if (!animate || target is null || Value is null || Value == target)
        {
            Stop(); Value = target; present(Value); return;
        }
        _from = Value.Value; _target = target.Value; _elapsed = 0; _duration = MotionSettings.ValueSeconds;
        _subscription ??= AnimationClock.Current.Subscribe(Frame);
    }
    private void Frame(double seconds)
    {
        _elapsed += seconds;
        var t = Math.Clamp(_elapsed / _duration, 0, 1);
        Value = _from + (_target - _from) * (1 - Math.Pow(1 - t, 3));
        present(Value);
        if (t >= 1) Stop();
    }
    private void Stop() { _subscription?.Dispose(); _subscription = null; }
    public void Dispose() { Stop(); _disposed = true; }
}
