using System.Windows;
using System.Windows.Media;
using TokNotch.UI.Animations;
namespace TokNotch.UI.Controls;

/// <summary>
/// "How much is left" gauge. A null fraction draws only the neutral track and never a zero-length value.
/// Optional inner ring carries the second Codex window; outer stays slightly thicker so the two are separable by weight and colour.
/// </summary>
public sealed class RingChart : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(nameof(Fraction), typeof(double?), typeof(RingChart), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty InnerFractionProperty = DependencyProperty.Register(nameof(InnerFraction), typeof(double?), typeof(RingChart), new PropertyMetadata(null, InnerChanged));
    public static readonly DependencyProperty ShowInnerProperty = DependencyProperty.Register(nameof(ShowInner), typeof(bool), typeof(RingChart), new PropertyMetadata(false, InnerChanged));
    public static readonly DependencyProperty IdentityProperty = DependencyProperty.Register(nameof(Identity), typeof(string), typeof(RingChart), new PropertyMetadata("", IdentityChanged));
    public static readonly DependencyProperty AnimationEnabledProperty = DependencyProperty.Register(nameof(AnimationEnabled), typeof(bool), typeof(RingChart), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(RingChart), new FrameworkPropertyMetadata(Brushes.Tan, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty InnerAccentProperty = DependencyProperty.Register(nameof(InnerAccent), typeof(Brush), typeof(RingChart), new FrameworkPropertyMetadata(Brushes.Tan, FrameworkPropertyMetadataOptions.AffectsRender));
    public double? Fraction { get => (double?)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public double? InnerFraction { get => (double?)GetValue(InnerFractionProperty); set => SetValue(InnerFractionProperty, value); }
    public bool ShowInner { get => (bool)GetValue(ShowInnerProperty); set => SetValue(ShowInnerProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public Brush InnerAccent { get => (Brush)GetValue(InnerAccentProperty); set => SetValue(InnerAccentProperty, value); }
    public string Identity { get => (string)GetValue(IdentityProperty); set => SetValue(IdentityProperty, value); }
    public bool AnimationEnabled { get => (bool)GetValue(AnimationEnabledProperty); set => SetValue(AnimationEnabledProperty, value); }
    private AnimatedScalar? _animation;
    private AnimatedScalar? _innerAnimation;
    private double? _displayed;
    private double? _displayedInner;
    private Pen Track=>new(TryFindResource("RingTrack") as Brush??Brushes.DimGray,5.5);
    private Pen InnerTrack=>new(TryFindResource("RingTrack") as Brush??Brushes.DimGray,4);
    public double? DisplayedFraction => _displayed;
    public double? DisplayedInnerFraction => _displayedInner;
    public bool IsAnimating => _animation?.IsRunning == true || _innerAnimation?.IsRunning == true;
    public RingChart()
    {
        Loaded += (_, _) => Update(false);
        Unloaded += (_, _) => { _animation?.Dispose(); _animation = null; _innerAnimation?.Dispose(); _innerAnimation = null; };
    }
    private static Pen MakeTrack(double thickness, byte alpha) { var brush = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255)); brush.Freeze(); var pen = new Pen(brush, thickness); pen.Freeze(); return pen; }
    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args) => ((RingChart)element).Update(false);
    private static void InnerChanged(DependencyObject element, DependencyPropertyChangedEventArgs args) => ((RingChart)element).UpdateInner(false);
    private static void IdentityChanged(DependencyObject element, DependencyPropertyChangedEventArgs args) => ((RingChart)element).Replay();
    internal void Replay() { Update(true); UpdateInner(true); }
    private void Update(bool reset)
    {
        _animation ??= new AnimatedScalar(value => { _displayed = value; InvalidateVisual(); });
        Apply(_animation, Fraction, reset, _displayed is null);
    }
    private void UpdateInner(bool reset)
    {
        if (!ShowInner) { _innerAnimation?.Dispose(); _innerAnimation = null; _displayedInner = null; InvalidateVisual(); return; }
        _innerAnimation ??= new AnimatedScalar(value => { _displayedInner = value; InvalidateVisual(); });
        Apply(_innerAnimation, InnerFraction, reset, _displayedInner is null);
    }
    private void Apply(AnimatedScalar animation, double? source, bool reset, bool displayedUnknown)
    {
        var target = source is double value && double.IsFinite(value) ? Math.Clamp(value, 0, 1) : (double?)null;
        var animate = IsLoaded && AnimationEnabled;
        if ((reset || displayedUnknown) && target.HasValue && animate) animation.Set(0, false);
        animation.Set(target, animate);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 6);
        if (!ShowInner)
        {
            dc.DrawEllipse(null, Track, center, radius, radius);
            DrawArc(dc, center, radius, _displayed, Accent, 5.5);
            return;
        }
        var innerRadius = Math.Max(0, radius - 10);
        dc.DrawEllipse(null, Track, center, radius, radius);
        dc.DrawEllipse(null, InnerTrack, center, innerRadius, innerRadius);
        DrawArc(dc, center, radius, _displayed, Accent, 5.5);
        DrawArc(dc, center, innerRadius, _displayedInner, InnerAccent, 4);
    }
    private static void DrawArc(DrawingContext dc, Point center, double radius, double? displayed, Brush accent, double thickness)
    {
        if (displayed is not double fraction || !double.IsFinite(fraction) || fraction <= 0 || radius <= 0) return;
        fraction = Math.Clamp(fraction, 0, 1);
        var pen = new Pen(accent, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var halo = new Pen(accent, thickness + 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (fraction >= .9999) { dc.PushOpacity(.16); dc.DrawEllipse(null, halo, center, radius, radius); dc.Pop(); dc.DrawEllipse(null, pen, center, radius, radius); return; }
        var angle = fraction * Math.PI * 2;
        var path = new StreamGeometry();
        using (var context = path.Open())
        {
            context.BeginFigure(new Point(center.X, center.Y - radius), false, false);
            context.ArcTo(new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius), new Size(radius, radius), 0, fraction > .5, SweepDirection.Clockwise, true, false);
        }
        path.Freeze();dc.PushOpacity(.16);dc.DrawGeometry(null,halo,path);dc.Pop();dc.DrawGeometry(null, pen, path);
    }
}
