namespace TokNotch.Core.Models;

public enum GlassFrameRateMode { Fps15, Fps30, Fps60, Custom, Display }

public sealed record GlassFrameRate(GlassFrameRateMode Mode=GlassFrameRateMode.Fps30,int CustomFps=30)
{
 public int FramesPerSecond=>Mode switch {GlassFrameRateMode.Fps15=>15,GlassFrameRateMode.Fps30=>30,GlassFrameRateMode.Fps60=>60,GlassFrameRateMode.Custom=>CustomFps,_=>0};
 public void Validate()
 {
  if(!Enum.IsDefined(Mode))throw new ArgumentException("玻璃采样档位无效。");
  if(CustomFps<1||CustomFps>360)throw new ArgumentException("自定义玻璃采样帧率需在 1–360 FPS 之间。");
 }
 // Zero means the existing WPF presentation handshake paces capture to the display.
 public static int Resolve(WindowPreferences settings,bool expanded,bool moving)=>moving?0:(expanded?settings.ExpandedRate:settings.CollapsedRate).FramesPerSecond;
 public static int DelayMilliseconds(int fps,double elapsedMilliseconds)=>fps==0?0:(int)Math.Ceiling(Math.Max(0,1000d/fps-elapsedMilliseconds));
}
