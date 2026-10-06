using TokNotch.Core.Animation;
using TokNotch.Core.Models;
namespace TokNotch.UI.Animations;

public sealed class IslandMorphController(Action<double> apply) : IDisposable
{
    private readonly ScalarSpring _spring = new();
    private IDisposable? _subscription;
    private bool _disposed;
    private bool _gentle;
    private double _gentleStart,_gentleTime;
    public double Progress => Math.Clamp(_spring.Value, 0, 1);
    public double ShapeProgress=>Math.Clamp(_spring.Value,IslandGeometry.MinimumSpringProgress,IslandGeometry.MaximumSpringProgress);
    public double Velocity => _spring.Velocity;
    public bool IsRunning => _subscription is not null;
    public event Action<bool>? Settled;
    public void Animate(bool expanded, bool reduceMotion = false, bool gentle = false)
    {
        if (_disposed) return;
        _gentle=gentle;_gentleDestination=expanded;_gentleStart=Progress;_gentleTime=0;
        _spring.Retarget(expanded ? 1 : 0, expanded ? MotionSettings.ExpandFrequency : MotionSettings.CollapseFrequency,gentle?1:expanded?.68:.76);
        if (reduceMotion || _spring.IsSettled) { SetImmediate(expanded); return; }
        _subscription ??= AnimationClock.Current.Subscribe(Frame);
    }
    private void Frame(double seconds)
    {
        if(_gentle)
        {
            _gentleTime+=seconds;var amount=Math.Clamp(_gentleTime/.12,0,1);var eased=amount*amount*(3-2*amount);
            _spring.Snap(_gentleStart+((_gentleDestination?1:0)-_gentleStart)*eased);
            // Snap also sets Target, so keep the destination separately during a gentle transition.
            if(amount>=1){SetImmediate(_gentleDestination);return;}
            _spring.Retarget(_gentleDestination?1:0);apply(Progress);return;
        }
        _spring.Advance(seconds);
        if (_spring.IsSettled) { SetImmediate(_spring.Target > .5); return; }
        apply(ShapeProgress);
    }
    public void SetImmediate(bool expanded)
    {
        if (_disposed) return;
        Stop(); _spring.Snap(expanded ? 1 : 0); apply(Progress); Settled?.Invoke(expanded);
    }
    private void Stop() { _subscription?.Dispose(); _subscription = null; }
    private bool _gentleDestination;
    public void Dispose() { Stop(); _disposed = true; Settled = null; }
}

