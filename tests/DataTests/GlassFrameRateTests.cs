using System.Text.Json;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Settings;
internal static class GlassFrameRateTests
{
 internal static void Run(string root)
 {
  int count=0;void Check(bool okay,string name){if(!okay)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
  var defaults=new WindowPreferences();
  Check(defaults.ExpandedRate.FramesPerSecond==30&&defaults.CollapsedRate.FramesPerSecond==30,"legacy glass settings default both idle states to 30 FPS");
  foreach(var mode in Enum.GetValues<GlassFrameRateMode>()) {
   var rate=new GlassFrameRate(mode,47);var p=defaults with{ExpandedGlassRate=rate,CollapsedGlassRate=new(GlassFrameRateMode.Fps15)};
   var folder=Path.Combine(root,"glass-rate-"+mode);var store=new DisplayPreferencesStore(folder);store.Save(DisplayPreferences.Default.WithWindow(p));
   Check(store.Load().Window==p,"glass "+mode+" mode and custom FPS survive restart");
   Check(GlassFrameRate.Resolve(p,true,false)==rate.FramesPerSecond&&GlassFrameRate.Resolve(p,false,false)==15,"expanded and collapsed rates are independent for "+mode);
   Check(GlassFrameRate.Resolve(p,false,true)==0&&GlassFrameRate.Resolve(p,true,true)==0,"motion follows display even with "+mode+" idle cap");
  }
  bool Invalid(GlassFrameRate rate){try{rate.Validate();return false;}catch(ArgumentException){return true;}}
  Check(Invalid(new(GlassFrameRateMode.Custom,0))&&Invalid(new(GlassFrameRateMode.Custom,361))&&Invalid(new((GlassFrameRateMode)99)),"invalid frame rate cannot be saved");
  Check(GlassFrameRate.DelayMilliseconds(15,0)==67&&GlassFrameRate.DelayMilliseconds(30,34)==0&&GlassFrameRate.DelayMilliseconds(60,10)==7&&GlassFrameRate.DelayMilliseconds(0,0)==0,"capture pacing handles caps, overdue work and display mode");
  var legacy=JsonSerializer.Deserialize<WindowPreferences>("{\"Theme\":1,\"Offset\":0.3}")!;
  Check(legacy.Theme==AppearanceTheme.Light&&legacy.Offset==.3&&legacy.ExpandedRate.FramesPerSecond==30,"new defaults preserve existing appearance and position");
  Console.WriteLine($"{count} glass frame rate checks passed");
 }
}
