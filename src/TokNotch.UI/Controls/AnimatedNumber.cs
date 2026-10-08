using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TokNotch.UI.Animations;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI.Controls;

public sealed class AnimatedNumber : TextBlock
{
    public static readonly DependencyProperty NumberProperty = DependencyProperty.Register(nameof(Number), typeof(double?), typeof(AnimatedNumber), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(string), typeof(AnimatedNumber), new PropertyMetadata("Currency", Changed));
    public static readonly DependencyProperty AnimationEnabledProperty = DependencyProperty.Register(nameof(AnimationEnabled), typeof(bool), typeof(AnimatedNumber), new PropertyMetadata(false, Changed));
    private AnimatedScalar? _animation;
    public double? Number { get => (double?)GetValue(NumberProperty); set => SetValue(NumberProperty, value); }
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public bool AnimationEnabled { get => (bool)GetValue(AnimationEnabledProperty); set => SetValue(AnimationEnabledProperty, value); }
    public double? DisplayedValue => _animation?.Value;
    public bool IsAnimating => _animation?.IsRunning == true;

    public AnimatedNumber()
    {
        Loaded += (_, _) => Update();
        Unloaded += (_, _) => { _animation?.Dispose(); _animation = null; };
    }
    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args) => ((AnimatedNumber)element).Update();
    private void Update()
    {
        _animation ??= new AnimatedScalar(Present);
        _animation.Set(Number, IsLoaded && AnimationEnabled && Format != "ResetTime");
    }
    private void Present(double? value)
    {
        Text = value is not double number ? "—" : Format switch
        {
            "Cny" => number.ToString("¥0.00", CultureInfo.InvariantCulture),
            "Ratio" => number.ToString("0.0'x'", CultureInfo.InvariantCulture),
            "Percent" => number.ToString("0%", CultureInfo.InvariantCulture),
            "ResetTime" => number is >= -62135596800 and <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds((long)number).LocalDateTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) : "—",
            "Compact" => number >= 1_000_000_000 ? (number / 1_000_000_000).ToString("0.0B", CultureInfo.InvariantCulture) : number >= 1_000_000 ? (number / 1_000_000).ToString("0.0M", CultureInfo.InvariantCulture) : number >= 1_000 ? (number / 1_000).ToString("0.0K", CultureInfo.InvariantCulture) : number.ToString("0", CultureInfo.InvariantCulture),
            _ => number.ToString("$0.00", CultureInfo.InvariantCulture)
        };
    }
}
