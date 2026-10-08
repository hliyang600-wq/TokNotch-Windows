using System.Windows;
using System.Windows.Media;
namespace TokNotch.UI.Animations;

/// <summary>Position-driven reveal; reversing the morph retraces the same path.</summary>
public sealed class StaggerController(params FrameworkElement[] rows)
{
    public void Apply(double progress, bool reduceMotion)
    {
        for (var i = 0; i < rows.Length; i++)
        {
            // Wait until the silhouette covers the fixed text layout, avoiding clipped half-glyphs.
            var start = .80 + .04 * i;
            var amount = reduceMotion ? progress : Math.Clamp((progress - start) / (1 - start), 0, 1);
            var eased = amount * amount * (3 - 2 * amount);
            // Reveal opacity only. Translating small glyphs through fractional device pixels
            // rerasterizes their stems and made the previous eight-DIP slide shimmer.
            rows[i].Opacity = eased;
        }
    }
}
