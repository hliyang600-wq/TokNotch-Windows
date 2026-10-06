using System.Diagnostics;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TokNotch.Core.Models;
using TokNotch.Core.Interaction;
namespace TokNotch.UI.Glass;
internal static class GlassFrameRateValidation
{
 internal static async Task RunAsync(IslandWindow island)
 {
  var background=GlassValidation.Background(island,false);var canvas=(Canvas)background.Content;int tick=0;
  var timer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(5)};
  timer.Tick+=(_,_)=>canvas.Background=new SolidColorBrush(Color.FromRgb((byte)(40+tick++%150),91,135));timer.Start();
  var glass=island.Glass!;var results=new List<object>();var checks=new List<string>();
  void Check(bool okay,string name){if(!okay)throw new InvalidOperationException(name+"; "+glass.Status);checks.Add(name);}
  try {
   island.MotionPreferenceOverride=true;island.Policy.SetMode(ExpansionMode.AlwaysExpanded);
   await Task.Delay(800);
   foreach(var fps in new[]{15,30,60,47}) {
    var rate=new GlassFrameRate(GlassFrameRateMode.Custom,fps);
    island.ApplyPreferences(DisplayPreferences.Default.WithWindow(new(ExpandedGlassRate:rate,CollapsedGlassRate:new(GlassFrameRateMode.Fps15))));
    island.Policy.SetMode(ExpansionMode.AlwaysExpanded);Check(glass.TargetFrameRate==fps,"expanded idle state selects "+fps+" FPS");
    await Task.Delay(250);var calls=glass.CaptureCalls;var uploads=glass.Uploads;var queries=glass.MonitorQueries;var watch=Stopwatch.StartNew();await Task.Delay(1600);var seconds=watch.Elapsed.TotalSeconds;
    int count=glass.CaptureCalls-calls,presentations=glass.Uploads-uploads;
    results.Add(new{RequestedFps=fps,Seconds=seconds,CaptureCalls=count,Uploads=presentations,MonitorQueries=glass.MonitorQueries-queries,glass.Status});
    Check(glass.MonitorQueries==queries,"steady capture performs no per-frame monitor lookup at "+fps+" FPS");
    Check(count<=Math.Ceiling(seconds*fps)+2&&presentations>3,"actual sampling respects "+fps+" FPS cap and keeps updating");
   }
   island.ApplyPreferences(DisplayPreferences.Default.WithWindow(new(ExpandedGlassRate:new(GlassFrameRateMode.Display),CollapsedGlassRate:new(GlassFrameRateMode.Fps15))));
   island.Policy.SetMode(ExpansionMode.AlwaysExpanded);
   Check(glass.TargetFrameRate==0,"display choice removes the idle cap");
   island.Policy.SetMode(ExpansionMode.Click);Check(glass.TargetFrameRate==15,"compact state applies its independent cap");
   island.MotionPreferenceOverride=false;island.Policy.Click();await Task.Delay(30);
   Check(glass.TargetFrameRate==0,"moving silhouette bypasses idle cap");
   island.Morph.SetImmediate(false);
   glass.SetSuspended(true);await Task.Delay(200);var before=glass.CaptureCalls;var beforeUploads=glass.Uploads;await Task.Delay(400);
   Check(glass.CaptureCalls==before&&glass.Uploads==beforeUploads,"suspension stops capture and presentation");
   glass.SetSuspended(false);await Task.Delay(1000);Check(glass.CaptureCalls>before&&glass.Uploads>beforeUploads,"resume recreates capture and updates the backdrop");
   glass.Enable(false);var stopped=glass.CaptureCalls;await Task.Delay(200);Check(!glass.CaptureActive&&glass.CaptureCalls==stopped,"frosted material has no running capture worker");
   glass.Enable(true);await Task.Delay(700);Check(glass.CaptureActive&&glass.CaptureCalls>stopped,"switching back to glass restarts capture");
   var frostedModel=new ViewModels.IslandViewModel();var defaults=DisplayPreferences.Default;
   frostedModel.Configure(new(defaults.Providers,defaults.Metrics,glassEnabled:false));
   var frosted=new IslandWindow(frostedModel);
   try{frosted.Show();await Task.Delay(150);Check(frosted.Glass is {CaptureActive:false,CaptureCalls:0},"frosted startup never creates a capture worker");}finally{frosted.Close();}
  }finally {
   timer.Stop();glass.SetSuspended(false);background.Close();
   var output=ApplicationPaths.ArtifactsDirectory;Directory.CreateDirectory(output);
   await File.WriteAllTextAsync(Path.Combine(output,"glass-rate-validation.json"),JsonSerializer.Serialize(new{Checks=checks,Measurements=results},new JsonSerializerOptions{WriteIndented=true}));
   island.Close();
  }
 }
}
