using TokNotch.Core.Abstractions;
namespace TokNotch.Core.Interaction;

public enum ExpansionMode { Hover, Click, AlwaysExpanded }
public enum IslandState { Collapsed, Expanding, Expanded, CollapsePending, Collapsing }

/// <summary>Pure interaction policy; timers are supplied by the desktop host.</summary>
public sealed class IslandStateController(IDeferredScheduler scheduler) : IDisposable
{
    private IDisposable? _pending;
    private bool _disposed;
    private bool _inside;
    public IslandState State { get; private set; } = IslandState.Collapsed;
    public ExpansionMode Mode { get; private set; }
    public bool TargetExpanded { get; private set; }
    public event Action<bool>? TargetChanged;
    public TimeSpan CollapseDelay { get; private set; } = TimeSpan.FromMilliseconds(300);
    public void SetCollapseDelay(TimeSpan delay)
    {
        if(delay.TotalMilliseconds is < 200 or > 1000)throw new ArgumentOutOfRangeException(nameof(delay));
        CollapseDelay=delay;
        if(State==IslandState.CollapsePending)PointerLeave();
    }

    public void PointerEnter()
    {
        if (_disposed) return;
        _inside = true; CancelPending();
        if (Mode == ExpansionMode.Hover) SetTarget(true);
    }
    public void PointerLeave()
    {
        if (_disposed) return;
        _inside = false;
        if (Mode != ExpansionMode.Hover || !TargetExpanded) return;
        CancelPending(); State = IslandState.CollapsePending;
        _pending = scheduler.Schedule(CollapseDelay, () =>
        {
            _pending = null;
            if (!_disposed && !_inside && Mode == ExpansionMode.Hover) SetTarget(false);
        });
    }
    public void Click()
    {
        if (!_disposed && Mode == ExpansionMode.Click) SetTarget(!TargetExpanded);
    }
    public void SetMode(ExpansionMode mode)
    {
        if (_disposed) return;
        CancelPending(); Mode = mode;
        SetTarget(mode == ExpansionMode.AlwaysExpanded || mode == ExpansionMode.Hover && _inside);
    }
    public void ShowExpanded() { if (!_disposed) SetTarget(true); }
    public void AnimationSettled(bool expanded)
    {
        if (_disposed || expanded != TargetExpanded || State == IslandState.CollapsePending) return;
        State = expanded ? IslandState.Expanded : IslandState.Collapsed;
    }
    private void SetTarget(bool expanded)
    {
        CancelPending();
        if (TargetExpanded == expanded)
        {
            if (State == IslandState.CollapsePending) State = IslandState.Expanded;
            return;
        }
        TargetExpanded = expanded; State = expanded ? IslandState.Expanding : IslandState.Collapsing;
        TargetChanged?.Invoke(expanded);
    }
    private void CancelPending() { _pending?.Dispose(); _pending = null; }
    public void Dispose() { _disposed = true; CancelPending(); TargetChanged = null; }
}
