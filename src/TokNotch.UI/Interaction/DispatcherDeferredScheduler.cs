using System.Windows.Threading;
using TokNotch.Core.Abstractions;
namespace TokNotch.UI.Interaction;

public sealed class DispatcherDeferredScheduler(Dispatcher dispatcher) : IDeferredScheduler
{
    public IDisposable Schedule(TimeSpan delay, Action action) => new ScheduledAction(dispatcher, delay, action);
    private sealed class ScheduledAction : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private Action? _action;
        public ScheduledAction(Dispatcher dispatcher, TimeSpan delay, Action action)
        {
            _action = action; _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = delay };
            _timer.Tick += Tick; _timer.Start();
        }
        private void Tick(object? sender, EventArgs args) { var action = _action; Dispose(); action?.Invoke(); }
        public void Dispose() { _timer.Stop(); _timer.Tick -= Tick; _action = null; }
    }
}
