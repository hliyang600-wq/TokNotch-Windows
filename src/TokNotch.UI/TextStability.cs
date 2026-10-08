using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TokNotch.Core.Interaction;
namespace TokNotch.UI;
internal static class TextStability
{
 internal static async Task RunAsync(IslandWindow window,string output)
 {
  var samples=new List<Sample>();
  window.MotionPreferenceOverride=false;
  window.Policy.SetMode(ExpansionMode.AlwaysExpanded); await Task.Delay(700);
  var expanded=(FrameworkElement)window.FindName("Expanded");
  var compact=(FrameworkElement)window.FindName("Compact");
  var title=(FrameworkElement)((Panel)window.FindName("HeaderRow")).Children[1];
  var today=(FrameworkElement)window.FindName("TodayNumber");
  var tokens=(FrameworkElement)window.FindName("TokensNumber");
  window.UpdateLayout();
  var baseline=new[]{title,today,tokens,compact}.Select(element=>element.PointToScreen(new Point(0,0))).ToArray();
  var clock=Stopwatch.StartNew(); TimeSpan last=TimeSpan.MinValue;
  EventHandler observe=(_,args)=> {
   var time=((RenderingEventArgs)args).RenderingTime; if(time==last) return; last=time;
   double progress=window.Morph.Progress; if(progress<=.01||progress>=.99) return;
   window.UpdateLayout();
   var points=new[]{title,today,tokens,compact}.Select(element=>element.PointToScreen(new Point(0,0))).ToArray();
   samples.Add(new Sample(clock.Elapsed.TotalMilliseconds,progress,points.Select(p=>p.X).ToArray(),points.Select(p=>p.Y).ToArray()));
  };
  CompositionTarget.Rendering+=observe;
  try {
   window.Policy.SetMode(ExpansionMode.Click); await Task.Delay(650);
   window.Policy.Click(); await Task.Delay(650);
   for(int i=0;i<6;i++) { window.Policy.Click(); await Task.Delay(70); }
   window.Policy.SetMode(ExpansionMode.AlwaysExpanded); await Task.Delay(650);
  } finally { CompositionTarget.Rendering-=observe; }
  double drift=samples.Count==0?double.PositiveInfinity:samples.Max(s=>Enumerable.Range(0,baseline.Length).Max(i=>Math.Max(Math.Abs(s.X[i]-baseline[i].X),Math.Abs(s.Y[i]-baseline[i].Y))));
  bool passed=samples.Count>=10&&drift<.01;
  var result=new { Passed=passed, SampleCount=samples.Count,MaxScreenPixelDrift=drift,LiveGlass=window.Glass is not null,ElementOrder=new[]{"title","today","tokens","compact"}, Dpi=VisualTreeHelper.GetDpi(window).PixelsPerDip,TextFormatting=TextOptions.GetTextFormattingMode(title).ToString(),TextRendering=TextOptions.GetTextRenderingMode(title).ToString(),Samples=samples, Note="Native screen coordinates during morph and rapid reversals. Data is held constant; opacity remains animated. This verifies layout stability, not subjective appearance." };
  await File.WriteAllTextAsync(Path.Combine(output,"text-stability.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true }));
  if(!passed) throw new InvalidOperationException($"Text moved during morph: {drift:0.###} screen pixels across {samples.Count} samples.");
 }
 private sealed record Sample(double TimeMs,double Progress,double[] X,double[] Y);
}
