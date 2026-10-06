using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using TokNotch.UI.Controls;
using TokNotch.Core.Models;
namespace TokNotch.UI.Glass;
internal sealed class GlassController : IDisposable
{
 private readonly IntPtr hwnd;
 private readonly IslandSurface surface;
 private readonly Dispatcher dispatcher;
 private CancellationTokenSource? cancellation;
 private Task? worker;
 private readonly ManualResetEventSlim presented=new(true);
 private readonly AutoResetEvent cadenceChanged=new(false);
 private readonly GlassPowerMonitor power;
 private int frameRate=30;
 private bool suspended;
 private bool disposed, affinity, enabled, subscribed;
 private int generation;
 private byte[]? pending;
 private int pendingWidth,pendingHeight;
 private TimeSpan previousRenderingTime=TimeSpan.MinValue;
 private double lastUploadSeconds;
 private readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
 internal List<double>? ValidationUploadTimes { get; set; }
 public string Status { get; private set; }="Frosted / recordable";
 public int Frames { get; private set; }
 public int Uploads { get; private set; }
 public int Readbacks { get; private set; }
 public int TextureAllocations { get; private set; }
 public int SkippedFrames { get; private set; }
 public double MaximumUploadIntervalMs { get; private set; }
 internal double CaptureMilliseconds { get; private set; }
 internal double PresentationWaitMilliseconds { get; private set; }
 internal int CaptureCalls { get; private set; }
 internal int MonitorQueries { get; private set; }
 internal bool CaptureActive=>worker is not null;
 internal byte[]? LastFrame { get; private set; }
 internal int LastWidth { get; private set; }
 internal int LastHeight { get; private set; }
 public GlassController(IntPtr hwnd,IslandSurface surface,bool initialGlass)
 {
  this.hwnd=hwnd; this.surface=surface; dispatcher=surface.Dispatcher;
  power=new(hwnd,SetSuspended);
  Enable(initialGlass);
 }
 internal int TargetFrameRate=>Volatile.Read(ref frameRate);
 internal bool Suspended=>Volatile.Read(ref suspended);
 internal void SetCadence(WindowPreferences preferences,bool expanded,bool moving)
 {
  var next=GlassFrameRate.Resolve(preferences,expanded,moving);
  if(Interlocked.Exchange(ref frameRate,next)!=next)cadenceChanged.Set();
 }
 internal void SetSuspended(bool value)
 {
  Volatile.Write(ref suspended,value);cadenceChanged.Set();
 }
 internal void Enable(bool value)
 {
  if(disposed) return;
  if(value==enabled&&(worker!=null||!value))return;
  StopWorker(); surface.ClearBackdrop(); LastFrame=null; enabled=value;
  if(affinity) SetWindowDisplayAffinity(hwnd,0); affinity=false;
  if(!value) { Status="Frosted / recordable"; return; }
  if(!surface.SupportsGlass) { Status="Fallback: GPU shader unavailable"; return; }
  if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,19041)) { Status="Fallback: Windows 10 2004 required"; return; }
  StartWorker();
 }
 private void StartWorker()
 {
  affinity=SetWindowDisplayAffinity(hwnd,0x11);
  if(!affinity) { Status=$"Fallback: capture exclusion unavailable ({Marshal.GetLastWin32Error()})"; return; }
  var source=new CancellationTokenSource(); cancellation=source; int current=generation;
  Status="Starting capture worker";
  worker=Task.Factory.StartNew(()=>CaptureLoop(current,source.Token),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
 }
 private void CaptureLoop(int current,CancellationToken token)
 {
  DesktopBackdrop? capture=null;
  long lastCapture=0;
  var waitHandles=new[]{token.WaitHandle,cadenceChanged};
  try {
   while(!token.IsCancellationRequested) {
    if(Suspended) {
     capture?.Dispose();capture=null;lastCapture=0;
     if(WaitHandle.WaitAny(waitHandles)==0)break;
     continue;
    }
    var delay=lastCapture==0?0:GlassFrameRate.DelayMilliseconds(TargetFrameRate,System.Diagnostics.Stopwatch.GetElapsedTime(lastCapture).TotalMilliseconds);
    if(delay>0) {if(WaitHandle.WaitAny(waitHandles,delay)==0)break;if(Suspended)continue;}
    if(lastCapture!=0&&GlassFrameRate.DelayMilliseconds(TargetFrameRate,System.Diagnostics.Stopwatch.GetElapsedTime(lastCapture).TotalMilliseconds)>0)continue;
    lastCapture=System.Diagnostics.Stopwatch.GetTimestamp();
    var started=System.Diagnostics.Stopwatch.GetTimestamp();
    byte[]? bytes; int width,height;
    try { capture??=new DesktopBackdrop(hwnd); bytes=capture.Capture(hwnd,out width,out height); }
    catch(Exception error) when(error.HResult==unchecked((int)0x887A0026)) {
     // DXGI access loss is a recoverable display/compositor transition. Keep the last image.
     capture?.Dispose(); capture=null; token.WaitHandle.WaitOne(50); continue;
    }
    CaptureMilliseconds+=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds; CaptureCalls++;
    Readbacks=capture.Readbacks; TextureAllocations=capture.TextureAllocations; SkippedFrames=capture.SkippedFrames;MonitorQueries=capture.MonitorQueries;
    if(bytes is null) continue;
    token.ThrowIfCancellationRequested();
    presented.Reset();
    // A single frame in flight: this buffer cannot be overwritten until the UI has copied it.
    dispatcher.BeginInvoke(DispatcherPriority.Render,()=> {
     if(disposed||current!=generation)return;
     if(Suspended) {presented.Set();return;}
     pending=bytes; pendingWidth=width; pendingHeight=height;
     if(!subscribed) { CompositionTarget.Rendering+=Present; subscribed=true; }
    });
    started=System.Diagnostics.Stopwatch.GetTimestamp(); presented.Wait(token); PresentationWaitMilliseconds+=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
   }
  } catch(OperationCanceledException) { }
  catch(Exception error) {
   dispatcher.BeginInvoke(DispatcherPriority.Background,()=> {
    if(disposed||current!=generation) return;
    surface.ClearBackdrop(); LastFrame=null; Status=$"Fallback: {error.GetType().Name} 0x{error.HResult:X8} at {error.Data["NativeOperation"]}";
    if(affinity) SetWindowDisplayAffinity(hwnd,0); affinity=false;
    _=RetryAsync(current);
   });
  }
  finally { capture?.Dispose(); }
 }
 private async Task RetryAsync(int current)
 {
  await Task.Delay(5000);
  if(!disposed&&enabled&&current==generation) { StopWorker(); StartWorker(); }
 }
 private void Present(object? sender,EventArgs args)
 {
  if(args is RenderingEventArgs rendering) {
   if(rendering.RenderingTime==previousRenderingTime) return;
   previousRenderingTime=rendering.RenderingTime;
  }
  CompositionTarget.Rendering-=Present; subscribed=false;
  try {
   if(disposed||Suspended||pending is null) return;
   surface.SetBackdrop(pending,pendingWidth,pendingHeight);
   LastFrame=pending; LastWidth=pendingWidth; LastHeight=pendingHeight;
   Frames++; Uploads++; Status="Live DXGI / native HLSL";
   double now=clock.Elapsed.TotalSeconds;
   if(lastUploadSeconds>0) MaximumUploadIntervalMs=Math.Max(MaximumUploadIntervalMs,(now-lastUploadSeconds)*1000);
   lastUploadSeconds=now; ValidationUploadTimes?.Add(now);
  } finally { pending=null; presented.Set(); }
 }
 private void StopWorker()
 {
  generation++; cancellation?.Cancel(); presented.Set();
  if(subscribed) { CompositionTarget.Rendering-=Present; subscribed=false; }
  pending=null;
  // The only potentially blocking operation is a 100 ms worker-side DXGI wait, not GPU work on the UI.
  // Do not reuse the hand-off gate while the previous owner is still active.
  worker?.GetAwaiter().GetResult(); worker=null; cancellation?.Dispose(); cancellation=null;
 }
 public void Dispose()
 {
  if(disposed) return; StopWorker(); disposed=true; enabled=false;
  if(affinity) SetWindowDisplayAffinity(hwnd,0); affinity=false;
  power.Dispose();LastFrame=null; presented.Dispose();cadenceChanged.Dispose();
 }
 [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
}
