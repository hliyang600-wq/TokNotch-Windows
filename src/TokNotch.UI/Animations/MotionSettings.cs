using System.Windows;
namespace TokNotch.UI.Animations;

public static class MotionSettings
{
    public static double ExpandFrequency => Read("ExpandFrequency", 28);
    public static double CollapseFrequency => Read("CollapseFrequency", 32);
    public static double ValueSeconds => Read("ValueMilliseconds", 320) / 1000;
    private static double Read(string key, double fallback) => Application.Current?.TryFindResource(key) is double value && double.IsFinite(value) && value > 0 ? value : fallback;
}
