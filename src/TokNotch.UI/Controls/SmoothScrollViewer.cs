using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TokNotch.UI.Animations;
namespace TokNotch.UI.Controls;

/// <summary>
/// Wheel scrolling glides to the target instead of jumping a fixed number of lines, and the wheel always
/// drives the page even when the pointer sits over a text box or combo box that would otherwise swallow it.
/// The glide rides the shared AnimationClock, so it ends with no rendering subscription left behind.
/// </summary>
public sealed class SmoothScrollViewer : ScrollViewer
{
    public static readonly DependencyProperty PixelsPerLineProperty = DependencyProperty.Register(nameof(PixelsPerLine),typeof(double),typeof(SmoothScrollViewer),new PropertyMetadata(40d));
    private const double TimeConstant = 0.055;
    private IDisposable? _subscription;
    private double _target;
    /// <summary>Pixels travelled per wheel line, multiplied by the system "lines per notch" setting.</summary>
    public double PixelsPerLine { get => (double)GetValue(PixelsPerLineProperty); set => SetValue(PixelsPerLineProperty, value); }
    public bool IsGliding => _subscription is not null;
    public SmoothScrollViewer()
    {
        PreviewMouseWheel += OnWheel;
        Unloaded += (_, _) => Stop();
    }
    private void OnWheel(object sender,MouseWheelEventArgs args)
    {
        if (ScrollableHeight <= 0) return;
        args.Handled = true;
        var lines = SystemParameters.WheelScrollLines;
        var step = lines < 0 ? Math.Max(1, ViewportHeight) : Math.Clamp(lines == 0 ? 3 : lines, 1, 8) * (double.IsFinite(PixelsPerLine) && PixelsPerLine > 0 ? PixelsPerLine : 40d);
        var origin = IsGliding ? _target : VerticalOffset;
        _target = Math.Clamp(origin + (args.Delta > 0 ? -step : step), 0, ScrollableHeight);
        if (_subscription is null)
        {
            _subscription = AnimationClock.Current.Subscribe(Frame);
            // The shared clock rides the render loop, and an idle WPF window stops producing frames:
            // dirtying the visual makes sure the first frame of the glide is actually pumped.
            InvalidateVisual();
        }
    }
    private void Frame(double seconds)
    {
        var position = VerticalOffset;
        var remaining = _target - position;
        if (Math.Abs(remaining) <= 0.4) { ScrollToVerticalOffset(_target); Stop(); return; }
        ScrollToVerticalOffset(position + remaining * (1 - Math.Exp(-seconds / TimeConstant)));
    }
    private void Stop() { _subscription?.Dispose(); _subscription = null; }
}

