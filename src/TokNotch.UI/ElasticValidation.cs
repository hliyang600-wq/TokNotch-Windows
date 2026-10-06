using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json;
using TokNotch.Core.Models;
using TokNotch.Core.Interaction;
using TokNotch.UI.Animations;
using TokNotch.UI.Controls;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;

internal static class ElasticValidation
{
 internal static async Task RunAsync(IslandWindow island,IslandViewModel model)
 {
  var original=model.Preferences;var reports=new List<object>();var surface=(IslandSurface)island.FindName("Surface");var title=((Grid)island.FindName("HeaderRow")).Children[0] as FrameworkElement;var value=(FrameworkElement)island.FindName("TodayNumber");
  try
  {
   island.MotionPreferenceOverride=false;
   foreach(var edge in Enum.GetValues<DockEdge>())
   {
    island.ApplyPreferences(original.WithWindow(original.Window with{Animation=AnimationMode.Normal,Edge=edge}));island.Policy.SetMode(ExpansionMode.Click);await Idle();island.UpdateLayout();
    var baseline=new[]{title!,value}.Select(element=>element.PointToScreen(new Point())).ToArray();
    double peak=0,minimum=1,drift=0;bool viewportSafe=true;int samples=0;
    var history=new List<object>();TimeSpan last=TimeSpan.MinValue;
    EventHandler observe=(_,args)=>
    {
     var time=((RenderingEventArgs)args).RenderingTime;if(time==last)return;last=time;
     var shape=island.Morph.ShapeProgress;peak=Math.Max(peak,shape);minimum=Math.Min(minimum,shape);samples++;
     island.UpdateLayout();var positions=new[]{title!,value}.Select(element=>element.PointToScreen(new Point())).ToArray();
     for(int i=0;i<positions.Length;i++)drift=Math.Max(drift,Math.Max(Math.Abs(positions[i].X-baseline[i].X),Math.Abs(positions[i].Y-baseline[i].Y)));
     var viewport=surface.BackdropViewport;viewportSafe&=viewport.X>=0&&viewport.Y>=0&&viewport.Right<=1.000001&&viewport.Bottom<=1.000001;
     history.Add(new{Shape=shape,Width=surface.MaterialWidth,Height=surface.MaterialHeight});
    };
    CompositionTarget.Rendering+=observe;
    try{island.Policy.Click();await Idle();island.Policy.Click();await Idle();}finally{CompositionTarget.Rendering-=observe;}
    bool passed=peak>1.035&&minimum<-.01&&samples>8&&drift<.01&&viewportSafe;
    reports.Add(new{Edge=edge.ToString(),Passed=passed,PeakProgress=peak,MinimumProgress=minimum,MaxTextDriftPixels=drift,ViewportSafe=viewportSafe,Samples=history});
    if(!passed)throw new InvalidOperationException($"Elastic validation failed on {edge}: peak={peak}, min={minimum}, drift={drift}, crop={viewportSafe}");
   }
  }
  finally
  {
   var output=ApplicationPaths.ArtifactsDirectory;await File.WriteAllTextAsync(Path.Combine(output,"elastic-motion.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
   island.ApplyPreferences(original);island.MotionPreferenceOverride=null;await Idle();
  }
 }
 private static async Task Idle(){for(int i=0;i<100&&AnimationClock.Current.ActiveCount>0;i++)await Task.Delay(15);}
}

