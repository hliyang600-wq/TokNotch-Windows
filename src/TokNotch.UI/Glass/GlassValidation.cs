using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TokNotch.Core.Interaction;
using TokNotch.Core.Models;
namespace TokNotch.UI.Glass;
internal static class GlassValidation
{
 internal static async Task RunPositionAsync(IslandWindow island)
 {
  var background=Background(island,false);var reports=new List<object>();bool passed=true;
  try
  {
   island.MotionPreferenceOverride=true;
   foreach(var edge in Enum.GetValues<DockEdge>())
   {
    island.ApplyPreferences(DisplayPreferences.Default.WithWindow(new(Edge:edge,Offset:.2)));island.Policy.SetMode(ExpansionMode.AlwaysExpanded);background.Left=island.Left-40;background.Top=island.Top-8;
    var color=Color.FromRgb((byte)(44+(int)edge*20),91,135);((Canvas)background.Content).Background=new SolidColorBrush(color);
    await Task.Delay(1200);var glass=island.Glass!;var frame=glass.LastFrame;int[]? rgb=null;
    if(frame!=null){int i=(glass.LastHeight/2*glass.LastWidth+glass.LastWidth/2)*4;rgb=new[]{(int)frame[i+2],(int)frame[i+1],(int)frame[i]};}
    bool okay=rgb!=null&&Math.Abs(rgb[0]-color.R)<=2&&Math.Abs(rgb[1]-color.G)<=2&&Math.Abs(rgb[2]-color.B)<=2;passed&=okay;
    TokNotch.Infrastructure.Windows.NativeMethods.GetWindowRect(island.Native!.Handle,out var rect);
    reports.Add(new{Edge=edge.ToString(),Passed=okay,glass.Status,Rgb=rgb,Left=rect.Left,Top=rect.Top,Width=rect.Right-rect.Left,Height=rect.Bottom-rect.Top});
   }
   var output=ApplicationPaths.ArtifactsDirectory;await File.WriteAllTextAsync(Path.Combine(output,"glass-position.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
   if(!passed)throw new InvalidOperationException("Docked glass background validation failed; see glass-position.json.");
  }
  finally{background.Close();island.Close();}
 }
 internal static Window Background(IslandWindow island,bool patterned)
 {
  var canvas=new Canvas { Background=new SolidColorBrush(Color.FromRgb(44,91,135)) };
  if(patterned) for(int x=0;x<480;x+=24) { var stripe=new System.Windows.Shapes.Rectangle { Width=12,Height=360,Fill=new SolidColorBrush(x%48==0?Color.FromRgb(175,69,130):Color.FromRgb(47,145,156)) }; Canvas.SetLeft(stripe,x); canvas.Children.Add(stripe); }
  var window=new Window { Title="TokNotch glass test backdrop", WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Left=island.Left-50,Top=island.Top-8,Width=480,Height=360,Content=canvas };
  window.Show(); Raise(island.Native!.Handle,new IntPtr(-1),0,0,0,0,0x13); return window;
 }
 internal static async Task RunAsync(IslandWindow island)
 {
  var output=ApplicationPaths.ArtifactsDirectory; Directory.CreateDirectory(output);
  var background=Background(island,false);
  try {
   island.MotionPreferenceOverride=true; island.Policy.SetMode(ExpansionMode.AlwaysExpanded);
   // Reposition the test backdrop after island expansion; it completely covers the sampled rectangle.
   background.Left=island.Left-50;
   await Task.Delay(2000);
   var glass=island.Glass!;
   var captureStartup=Stopwatch.StartNew();
   while(glass.LastFrame is null&&captureStartup.Elapsed<TimeSpan.FromSeconds(8)) await Task.Delay(100);
   var frame=glass.LastFrame;
   bool sampleMatches=false; int[]? center=null;
   if(frame is not null) { int i=((glass.LastHeight/2)*glass.LastWidth+glass.LastWidth/2)*4; center=new[]{(int)frame[i+2],(int)frame[i+1],(int)frame[i]}; sampleMatches=Math.Abs(center[0]-44)<=2&&Math.Abs(center[1]-91)<=2&&Math.Abs(center[2]-135)<=2; }
   var process=Process.GetCurrentProcess(); var cpu=process.TotalProcessorTime; int initial=glass.Frames;
   await Task.Delay(2000); process.Refresh();
   var liveStatus=glass.Status; int liveFrames=glass.Frames; int idleFrames=glass.Frames-initial; var idleCpu=(process.TotalProcessorTime-cpu).TotalMilliseconds;
   island.Policy.SetMode(ExpansionMode.Click); await Task.Delay(150);
   TokNotch.Infrastructure.Windows.NativeMethods.GetWindowRect(island.Native!.Handle,out var host);
   var backgroundHandle=((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(background)).Handle;
   bool transparentHost=WindowFromPoint(new PointI { X=host.Left+5,Y=host.Top+10 })==backgroundHandle;
   bool visiblePill=WindowFromPoint(new PointI { X=(host.Left+host.Right)/2,Y=host.Top+10 })==island.Native.Handle;
   island.Policy.SetMode(ExpansionMode.AlwaysExpanded);
   glass.Enable(false); GetAffinity(island.Native!.Handle,out var disabledAffinity);
   bool recordable=disabledAffinity==0&&glass.LastFrame is null;
   glass.Enable(true); await Task.Delay(900); bool resumed=glass.Frames>liveFrames;
   var originalHandle=island.Native!.Handle;
   island.MotionPreferenceOverride=false; island.Policy.SetMode(ExpansionMode.Click);
   for(int i=0;i<12;i++) { island.Policy.Click(); await Task.Delay(85); }
   island.Policy.SetMode(ExpansionMode.AlwaysExpanded); await Task.Delay(1000);
   bool morphStable=island.Native.Handle==originalHandle&&Math.Abs(island.Width-IslandGeometry.HostWidth)<1&&glass.Status=="Live DXGI / native HLSL";
   var surface=(TokNotch.UI.Controls.IslandSurface)island.FindName("Surface");
   int bitmapBefore=surface.BitmapAllocations, textureBefore=glass.TextureAllocations;
   var viewports=new List<object>(); bool viewportCorrect=true;
   using(var observation=TokNotch.UI.Animations.AnimationClock.Current.Subscribe(_=> {
    double p=island.Morph.Progress;
    if(p>.01&&p<.99) { var viewport=surface.BackdropViewport; viewportCorrect &= Math.Abs(viewport.Width-surface.MaterialWidth/IslandGeometry.HostWidth)<.00001&&Math.Abs(viewport.Height-surface.MaterialHeight/IslandGeometry.HostHeight)<.00001; viewports.Add(new { Progress=p,Width=island.Width,SurfaceWidth=surface.MaterialWidth,SurfaceHeight=surface.MaterialHeight,Viewport=viewport.ToString() }); }
   })) {
    island.Policy.SetMode(ExpansionMode.Click); await Task.Delay(500); island.Policy.Click(); await Task.Delay(650);
   }
   bool buffersReused=surface.BitmapAllocations==bitmapBefore&&glass.TextureAllocations==textureBefore;
   await TokNotch.UI.TextStability.RunAsync(island,output);
   // Animate our own uniform backdrop at WPF presentation cadence. Persist timings, not image data.
   var colorBrush=new SolidColorBrush(Color.FromRgb(44,91,135)); ((Canvas)background.Content).Background=colorBrush;
   int sourceFrames=0; TimeSpan lastRender=TimeSpan.MinValue; var dynamicClock=Stopwatch.StartNew();
   EventHandler animate=(_,args)=> { var time=((RenderingEventArgs)args).RenderingTime; if(time==lastRender) return; lastRender=time; sourceFrames++; colorBrush.Color=Color.FromRgb((byte)(44+(sourceFrames%100)),91,135); };
   glass.ValidationUploadTimes=new(); var dynamicCpu=process.TotalProcessorTime;
   CompositionTarget.Rendering+=animate;
   try { await Task.Delay(3000); } finally { CompositionTarget.Rendering-=animate; }
   double seconds=dynamicClock.Elapsed.TotalSeconds; process.Refresh();
   var times=glass.ValidationUploadTimes.ToArray(); glass.ValidationUploadTimes=null;
   var intervals=times.Zip(times.Skip(1),(a,b)=>(b-a)*1000).Order().ToArray();
   double p95=intervals.Length==0?0:intervals[(int)((intervals.Length-1)*.95)];
   await File.WriteAllTextAsync(Path.Combine(output,"glass-frame-sync.json"),JsonSerializer.Serialize(new { DisplayHz=DisplayHz(), SourceRenderFps=sourceFrames/seconds, UploadedFps=times.Length/seconds, UploadP95IntervalMs=p95, DynamicCpuMilliseconds=(process.TotalProcessorTime-dynamicCpu).TotalMilliseconds, BitmapAllocationsDuringMorph=surface.BitmapAllocations-bitmapBefore, TextureAllocationsDuringMorph=glass.TextureAllocations-textureBefore, ViewportCorrect=viewportCorrect,ViewportSamples=viewports, Readbacks=glass.Readbacks,SkippedDesktopFrames=glass.SkippedFrames,glass.CaptureMilliseconds,glass.PresentationWaitMilliseconds,glass.CaptureCalls,UploadTimesSeconds=times },new JsonSerializerOptions { WriteIndented=true }));
   bool dockedBackdrop=true;island.MotionPreferenceOverride=true;colorBrush.Color=Color.FromRgb(44,91,135);
   foreach(var side in Enum.GetValues<DockEdge>())
   {
    island.ApplyPreferences(DisplayPreferences.Default.WithWindow(new(Edge:side,Offset:.2)));island.Policy.SetMode(ExpansionMode.AlwaysExpanded);
    background.Left=island.Left-40;background.Top=island.Top-8;await Task.Delay(750);
    var current=glass.LastFrame;if(current==null){dockedBackdrop=false;continue;}
    int pixel=((glass.LastHeight/2)*glass.LastWidth+glass.LastWidth/2)*4;
    dockedBackdrop&=Math.Abs(current[pixel+2]-44)<=2&&Math.Abs(current[pixel+1]-91)<=2&&Math.Abs(current[pixel]-135)<=2;
   }
   var result=new { ShaderCompiledBytes=LiquidGlassEffect.Compile().Length, Status=liveStatus,Frames=liveFrames,glass.Uploads,CenterRgb=center,SelfExclusionVerified=sampleMatches,DockedBackdropVerified=dockedBackdrop,RecordableSwitchVerified=recordable,ResumeVerified=resumed,TransparentHostHitTestVerified=transparentHost,VisiblePillHitTestVerified=visiblePill,LiveMorphReversals=12,LiveMorphVerified=morphStable,AnimationViewportVerified=viewportCorrect&&viewports.Count>5,BuffersReused=buffersReused,IdleFrames=idleFrames,IdleCpuMilliseconds=idleCpu,WorkingSetMb=process.WorkingSet64/1048576d, RenderTier=RenderCapability.Tier>>16, ScreenshotNote="Only an authored solid backdrop is sampled in this validation; no desktop pixels are persisted." };
   await File.WriteAllTextAsync(Path.Combine(output,"glass-validation.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true }));
   bool refreshCadence=times.Length/seconds>=Math.Min(DisplayHz(),sourceFrames/seconds)*.8;
   if(frame is null||!sampleMatches||!dockedBackdrop||!recordable||!resumed||!morphStable||!buffersReused||!viewportCorrect||!transparentHost||!visiblePill||viewports.Count<=5||times.Length<20||!refreshCadence) throw new InvalidOperationException($"Live glass validation failed: {glass.Status}; center={string.Join(',',center??Array.Empty<int>())}; background fps={times.Length/seconds:0.0}; docked backdrop={dockedBackdrop}");
  } finally { background.Close(); island.Close(); }
 }
 [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowPos")] private static extern bool Raise(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
 [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowDisplayAffinity")] private static extern bool GetAffinity(IntPtr hwnd,out uint affinity);
 private static int DisplayHz() { var memory=System.Runtime.InteropServices.Marshal.AllocHGlobal(220); try { for(int i=0;i<220;i+=4) System.Runtime.InteropServices.Marshal.WriteInt32(memory,i,0); System.Runtime.InteropServices.Marshal.WriteInt16(memory,68,220); return EnumDisplaySettings(null,-1,memory)?System.Runtime.InteropServices.Marshal.ReadInt32(memory,184):0; } finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(memory); } }
 [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="EnumDisplaySettingsW",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] private static extern bool EnumDisplaySettings(string? name,int mode,IntPtr settings);
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct PointI { public int X,Y; }
 [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(PointI point);
}
