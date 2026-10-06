using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TokNotch.Infrastructure.Windows;
using TokNotch.Infrastructure.Usage;
using TokNotch.Core.Interaction;
using TokNotch.Core.Models;
using TokNotch.UI.Animations;
using TokNotch.UI.Controls;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;

internal static class Validation
{
    public static async Task RunAsync(IslandWindow window, IslandViewModel model, MockUsageSource source)
    {
        var output = ApplicationPaths.ArtifactsDirectory;
        Directory.CreateDirectory(output);
        var report = Path.Combine(output, "window-validation.json");
        if (File.Exists(report)) File.Delete(report);
        var checks = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        var handle = window.Native!.Handle;
        var foreground = NativeMethods.GetForegroundWindow();
        var dpi = NativeMethods.GetDpiForWindow(handle);
        var extendedStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        await TextStability.RunAsync(window, output);
        var activation = NativeMethods.SendMessage(handle, NativeMethods.WmMouseActivate, IntPtr.Zero, IntPtr.Zero).ToInt32();
        window.MotionPreferenceOverride = false;
        window.Policy.SetMode(ExpansionMode.Click);
        for (var i = 0; i < 100; i++)
        {
            window.Policy.Click(); window.Morph.SetImmediate(window.Policy.TargetExpanded);
            Check(window.Native.Handle == handle, "HWND cycle " + i);
        }
        var material = (IslandSurface)window.FindName("Surface");
        var timeline = new List<string> { "milliseconds,progress,velocity,visibleWidth,visibleHeight,activeAnimations" };
        var clock = Stopwatch.StartNew();
        window.Policy.Click();
        for (var i = 0; i < 8; i++)
        {
            await Task.Delay(35);
            timeline.Add(FormattableString.Invariant($"{clock.Elapsed.TotalMilliseconds:0.0},{window.Morph.Progress:0.0000},{window.Morph.Velocity:0.0000},{material.MaterialWidth:0.00},{material.MaterialHeight:0.00},{AnimationClock.Current.ActiveCount}"));
            await Capture(window, output, "morph-" + i, 1);
        }
        var position = window.Morph.Progress; var velocity = window.Morph.Velocity;
        window.Policy.Click();
        Check(window.Morph.Progress == position && window.Morph.Velocity == velocity, "retarget preserves visible position and velocity");
        for (var i = 0; i < 20; i++) { window.Policy.Click(); await Task.Delay(15); }
        window.Policy.SetMode(ExpansionMode.AlwaysExpanded);
        await WaitIdle();
        Check(Math.Abs(window.Width - IslandGeometry.HostWidth) < 1 && Math.Abs(((FrameworkElement)window.Content).Width - IslandGeometry.HostWidth) < 1, "window and surface settle together");
        Check(((FrameworkElement)window.FindName("ProviderRow")).Opacity == 1, "stagger ends fully visible");
        foreach (var scale in new[] { 1d, 1.25, 1.5, 1.75, 2 }) await Capture(window, output, $"expanded-{scale:0.##}", scale);

        var today = (AnimatedNumber)window.FindName("TodayNumber");
        var month = (AnimatedNumber)window.FindName("MonthNumber");
        var ring = (RingChart)window.FindName("UsageRing");
        model.Select(Provider.OpenAI); await Task.Delay(80);
        Check(today.DisplayedValue is > 6.4 and < 18.2 && today.IsAnimating, "number interpolates between provider values");
        Check(ring.Identity.StartsWith("demo.codex:", StringComparison.Ordinal) && ring.IsAnimating && ring.DisplayedFraction is >= 0 and < .46, "new ring identity starts independent animation");
        await Capture(window, output, "codex-transition", 1);
        model.Select(Provider.Gemini); await Task.Delay(45); model.Select(Provider.Claude);
        await WaitIdle();
        Check(Math.Abs(today.DisplayedValue!.Value - 18.20) < .0001 && Math.Abs(ring.DisplayedFraction!.Value - .72) < .0001, "rapid provider retarget settles at latest snapshot");
        model.Select(Provider.OpenAI); await Task.Delay(40);
        source.Scenario = DemoScenario.PriceUnavailable; model.Apply(await source.GetUsageAsync());
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        Check(today.DisplayedValue is null && today.Text == "—" && month.DisplayedValue is null && ring.DisplayedFraction is null, "unknown prices clear animated values immediately");
        await WaitIdle();
        Check(AnimationClock.Current.ActiveCount == 0, "unknown data leaves no animation subscription");

        model.Select(Provider.Claude);
        foreach (var scenario in Enum.GetValues<DemoScenario>())
        {
            source.Scenario = scenario; model.Apply(await source.GetUsageAsync()); await WaitIdle();
            await Capture(window, output, scenario.ToString(), 1);
        }
        source.Scenario = DemoScenario.Normal; model.Apply(await source.GetUsageAsync()); await WaitIdle();
        window.Policy.SetMode(ExpansionMode.Click); await Task.Delay(40);
        Check(window.Morph.IsRunning, "collapse starts animation before preference changes");
        window.MotionPreferenceOverride = true; window.RefreshMotionPolicy();
        Check(window.Morph.Progress == 0 && AnimationClock.Current.ActiveCount == 0, "mid-animation reduced motion cancels and snaps");
        window.Policy.SetMode(ExpansionMode.Click);
        Check(window.Morph.Progress == 0 && !window.Morph.IsRunning, "reduce motion collapse snaps");
        await Capture(window, output, "collapsed", 1);
        window.Policy.SetMode(ExpansionMode.AlwaysExpanded); model.Select(Provider.Gemini);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        Check(window.Morph.Progress == 1 && !today.IsAnimating && today.DisplayedValue == model.TodayValue, "reduce motion numbers and morph snap");
        Check(AnimationClock.Current.ActiveCount == 0, "reduced motion keeps no rendering subscription");
        await Task.Delay(1500);
        Check(AnimationClock.Current.ActiveCount == 0, "open window idle has no subscriptions");
        var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
        await Task.Delay(2000); process.Refresh();
        var cpuDelta = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        var workingSet = process.WorkingSet64 / 1048576d;
        window.MotionPreferenceOverride = false; window.Policy.SetMode(ExpansionMode.Click); await Task.Delay(60); window.Close();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        Check(AnimationClock.Current.ActiveCount == 0, "closing during morph disposes all subscriptions");
        Check(activation == NativeMethods.MaNoActivate, "mouse activation response");
        var result = new
        {
            Checks = checks, HwndStable = true, Cycles = 100, Reversals = 20,
            MouseActivateResult = activation,
            ForegroundPreserved = foreground == NativeMethods.GetForegroundWindow(),
            Dpi = dpi, NoActivateStyle = (extendedStyle & NativeMethods.WsExNoActivate) != 0, DisplayCount = new MonitorService().DisplayCount, RenderTier = RenderCapability.Tier >> 16,
            IdleCpuMilliseconds = cpuDelta,
            WorkingSetMb = workingSet,
            ActiveAnimationSubscriptions = AnimationClock.Current.ActiveCount,
            AnimationFrames = AnimationClock.Current.FrameCount,
            MaximumObservedFrameIntervalMs = AnimationClock.Current.MaximumFrameIntervalMs,
            Note = "DPI PNGs are offscreen WPF renders; physical typing, click-through and multi-monitor tests remain unverified. Idle sampled while expanded and stationary. Frame interval is not an FPS benchmark."
        };
        await File.WriteAllLinesAsync(Path.Combine(output, "animation-timeline.csv"), timeline);
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task WaitIdle()
    {
        var clock = Stopwatch.StartNew();
        while (AnimationClock.Current.ActiveCount > 0 && clock.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(25);
        if (AnimationClock.Current.ActiveCount != 0) throw new InvalidOperationException("Animations did not settle");
        await Task.Delay(30);
    }
    private static async Task Capture(IslandWindow window, string output, string name, double scale)
    {
        window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(window.Width, window.Height)); content.Arrange(new Rect(0, 0, window.Width, window.Height)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.Width * scale), (int)Math.Ceiling(window.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(content); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
    }
}

