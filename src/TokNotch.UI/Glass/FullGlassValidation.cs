using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokNotch.Core.Models;
using TokNotch.Core.Interaction;
using TokNotch.UI.Controls;
namespace TokNotch.UI.Glass;
internal static class FullGlassValidation
{
 internal static async Task RunAsync(IslandWindow island)
 {
  _=LiquidGlassEffect.Compile();var surface=(IslandSurface)island.FindName("Surface");
  if(!surface.SupportsGlass)throw new InvalidOperationException("Native glass shader or maps unavailable.");
  var output=Path.Combine(ApplicationPaths.ArtifactsDirectory,"full-glass");Directory.CreateDirectory(output);
  island.MotionPreferenceOverride=true;island.Policy.SetMode(ExpansionMode.AlwaysExpanded);
  var background=GlassValidation.Background(island,true);background.Left=island.Left-50;background.Top=island.Top-8;
  background.Activate();Raise(island.Native!.Handle,new IntPtr(-1),0,0,0,0,0x13);
  var checks=new List<string>();
  try{
   await Task.Delay(2200);if(island.Glass?.LastFrame is null)throw new InvalidOperationException("Live backdrop unavailable: "+island.Glass?.Status);
   var frameWidth=island.Glass.LastWidth;var frameHeight=island.Glass.LastHeight;
   var originalFrame=island.Glass.LastFrame.ToArray();var originalBounds=surface.CaptureBounds;
   island.Glass.Dispose(); // Freeze our authored backdrop, then capture the actual GPU-rendered island.
   using var capture=new DesktopBackdrop(island.Native!.Handle);
   byte[]? previousShot=null;
   async Task<byte[]> Shot(string name,GlassMaterial material){surface.ConfigureMaterial(material,true);await Task.Delay(300);var bytes=capture.Capture(island.Native.Handle,out var w,out var h)??previousShot??throw new InvalidOperationException("GPU screenshot unavailable");var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgr32,null,bytes,w*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name+".png"));encoder.Save(file);previousShot=bytes.ToArray();return previousShot;}
   void Different(byte[] a,byte[] b,string label){double delta=0;for(int i=0;i<a.Length;i++)if(i%4!=3)delta+=Math.Abs(a[i]-b[i]);if(delta/a.Length<.03)throw new InvalidOperationException("Effect did not change actual GPU pixels: "+label);checks.Add(label);}
   var baseline=await Shot("standard",new());
   foreach(var mode in new[]{RefractionMode.Polar,RefractionMode.Prominent,RefractionMode.Shader})Different(baseline,await Shot(mode.ToString().ToLowerInvariant(),new(Mode:mode)),"GPU refraction "+mode);
   foreach(var item in new[]{("displacement",new GlassMaterial(Displacement:0)),("blur",new GlassMaterial(Blur:1)),("saturation",new GlassMaterial(Saturation:0)),("aberration",new GlassMaterial(Aberration:5)),("overlight",new GlassMaterial(OverLight:true)),("tint",new GlassMaterial(TintOpacity:.8)),("radius",new GlassMaterial(CornerRadius:0))})Different(baseline,await Shot(item.Item1,item.Item2),"GPU "+item.Item1);
   surface.ConfigureMaterial(new(),true);var anchor=((FrameworkElement)island.FindName("Expanded")).TranslatePoint(new(),island);
   surface.SetInteraction(new Point(150,25),true,false,false);
   if(Math.Abs(surface.HighlightOffset.X-150d/380*100)>1e-6||Math.Abs(surface.HighlightOffset.Y-25d/220*100)>1e-6)throw new InvalidOperationException("Highlight input must use original percentage units.");
   checks.Add("original percentage highlight input at expanded size");
   surface.SetShape(180,32,16);surface.SetInteraction(new Point(45,8),true,false,false);
   if(Math.Abs(surface.HighlightOffset.X-25)>1e-6||Math.Abs(surface.HighlightOffset.Y-25)>1e-6)throw new InvalidOperationException("Compact highlight normalization incorrect.");
   checks.Add("original percentage highlight input at compact size");surface.SetShape(380,220,28);surface.SetInteraction(new(),false,false,false);
   if(((FrameworkElement)surface.Child!).Effect is not null)throw new InvalidOperationException("Content container must not cast a shadow.");
   checks.Add("text shadow excludes content container and ring graphics");
   if(((System.Windows.Controls.TextBlock)island.FindName("RingCenterTop")).Effect is not System.Windows.Media.Effects.DropShadowEffect {BlurRadius:18,ShadowDepth:2}||surface.Effect is not System.Windows.Media.Effects.DropShadowEffect {BlurRadius:60})throw new InvalidOperationException("CSS shadow blur radius conversion incorrect.");
   checks.Add("CSS text/box shadow radii map to native Gaussian sigma");
   surface.SetInteraction(new Point(150,25),true,false,true);await Task.Delay(400);var hover=await Shot("hover",new());Different(baseline,hover,"hover stretch and highlights");
   surface.SetInteraction(new Point(150,25),true,true,true);await Task.Delay(400);Different(hover,await Shot("press",new()),"press response");
   if(((FrameworkElement)island.FindName("Expanded")).TranslatePoint(new(),island)!=anchor)throw new InvalidOperationException("Text anchor moved with material.");checks.Add("text anchor stays fixed during material stretch");
   surface.SetInteraction(new(),false,false,true);await Task.Delay(450);if(Animations.AnimationClock.Current.ActiveCount!=0)throw new InvalidOperationException("Material animations remain active at idle.");checks.Add("material animation subscriptions stop at idle");
   surface.Child!.Visibility=Visibility.Hidden;surface.SetShape(380,220,28);
   var sourceWidth=(int)IslandGeometry.HostWidth;var sourceHeight=(int)IslandGeometry.HostHeight;
   var step=new byte[sourceWidth*sourceHeight*4];
   for(int y=0;y<sourceHeight;y++)for(int x=sourceWidth/2;x<sourceWidth;x++)for(int c=0;c<3;c++)step[(y*sourceWidth+x)*4+c]=255;
   surface.SetBackdrop(step,sourceWidth,sourceHeight,new Rect(0,0,sourceWidth,sourceHeight));
   var gaussian=await Shot("gaussian-step",new(Displacement:0,Blur:.0625,Saturation:100,TintOpacity:0));
   var scale=VisualTreeHelper.GetDpi(island).DpiScaleX;var shotWidth=(int)Math.Round(island.Width*scale);
   int Sample(byte[] image,double x,double y,int channel)=>image[((int)Math.Round(y*scale)*shotWidth+(int)Math.Round(x*scale))*4+channel];
   var profile=Enumerable.Range(-4,9).Select(i=>Sample(gaussian,sourceWidth/2d+i*3,110,2)).ToArray();
   for(int i=1;i<profile.Length;i++)if(profile[i]<profile[i-1])throw new InvalidOperationException("Gaussian edge is not monotonic.");
   if(profile[2]<25||profile[2]>70||profile[6]<190||profile[6]>240)throw new InvalidOperationException("Gaussian sigma does not match CSS blur(6px): "+string.Join(",",profile));
   checks.Add("GPU Gaussian step profile matches 6px sigma: "+string.Join(",",profile));
   Array.Clear(step);for(int y=0;y<sourceHeight;y++)for(int x=0;x<112;x++)step[(y*sourceWidth+x)*4+2]=255;
   surface.SetShape(180,32,16);surface.SetBackdrop(step,sourceWidth,sourceHeight,new Rect(0,0,sourceWidth,sourceHeight));
   var compactBlur=await Shot("compact-outside-sampling",new(Displacement:0,Blur:.0625,Saturation:100,TintOpacity:0));
   var outside=Sample(compactBlur,118,16,2);if(outside<15||outside>90)throw new InvalidOperationException("Compact blur does not read pixels outside pill: "+outside);
   checks.Add("compact Gaussian reads outside pill: red="+outside);
   surface.SetBackdrop(originalFrame,frameWidth,frameHeight,originalBounds);surface.SetShape(380,220,28);
   if(originalBounds.Width<=IslandGeometry.HostWidth||originalBounds.Height<=IslandGeometry.HostHeight)throw new InvalidOperationException("Backdrop capture has no blur/refraction margin.");
   checks.Add("capture margin follows Gaussian radius and displacement: "+originalBounds);
   foreach(var shape in new[]{(180d,32d,16d),(280d,126d,22d),(380d,220d,28d)})foreach(var edge in Enum.GetValues<DockEdge>()){
    surface.SetShape(shape.Item1,shape.Item2,shape.Item3,edge);var mask=surface.OpacityMask;
    var masked=await Shot("mask-"+edge+"-"+(int)shape.Item1,new(TintOpacity:1));surface.OpacityMask=null;
    var plain=await Shot("plain-"+edge+"-"+(int)shape.Item1,new(TintOpacity:1));surface.OpacityMask=mask;
    var dpi=VisualTreeHelper.GetDpi(island).DpiScaleX;var imageWidth=(int)Math.Round(island.Width*dpi);double delta=0;int count=0;int missing=0;
    var centerX=edge==DockEdge.Left?shape.Item1/2:edge==DockEdge.Right?IslandGeometry.HostWidth-shape.Item1/2:IslandGeometry.HostWidth/2;var centerY=edge==DockEdge.Top?shape.Item2/2:edge==DockEdge.Bottom?IslandGeometry.HostHeight-shape.Item2/2:IslandGeometry.HostHeight/2;
    for(int y=0;y<IslandGeometry.HostHeight*dpi;y++)for(int x=0;x<imageWidth;x++){
     var qx=Math.Abs((x+.5)/dpi-centerX)-(shape.Item1/2-shape.Item3);var qy=Math.Abs((y+.5)/dpi-centerY)-(shape.Item2/2-shape.Item3);
     var d=Math.Sqrt(Math.Pow(Math.Max(qx,0),2)+Math.Pow(Math.Max(qy,0),2))+Math.Min(Math.Max(qx,qy),0)-shape.Item3;
     if(d>=-4)continue;var index=(y*imageWidth+x)*4;double difference=0;for(int c=0;c<3;c++)difference+=Math.Abs(masked[index+c]-plain[index+c]);delta+=difference/3;count++;if(difference>9)missing++;
    }
    var mean=delta/count;checks.Add("mask interior "+edge+" "+shape.Item1+"x"+shape.Item2+": mean="+mean.ToString("0.###")+", altered="+missing+"/"+count);
    await File.WriteAllLinesAsync(Path.Combine(output,"mask-interior-checks.txt"),checks);
    if(mean>.5)throw new InvalidOperationException("Mask crops opaque interior at "+shape.Item1+": "+mean);
   }
   if(surface.OpacityMask is not DrawingBrush||surface.Background==Brushes.Transparent)throw new InvalidOperationException("Native edge mask or stable material underlay missing.");
   checks.Add("native edge mask keeps the opaque material drawing path");
   var steady=await Shot("steady-mask",new(TintOpacity:1));var pixels=new byte[frameWidth*frameHeight*4];int tick=0,samples=0;double worst=0;
   using(var rendering=Animations.AnimationClock.Current.Subscribe(_=>{Array.Fill(pixels,(byte)(tick++%2==0?32:224));surface.SetBackdrop(pixels,frameWidth,frameHeight);})){
    for(int i=0;i<90;i++){
     await Task.Delay(17);var screen=capture.Capture(island.Native.Handle,out _,out _);if(screen is null)continue;samples++;double delta=0;for(int j=0;j<screen.Length;j++)if(j%4!=3)delta+=Math.Abs(screen[j]-steady[j]);worst=Math.Max(worst,delta/screen.Length);
    }
   }
   if(tick<60||worst>.1)throw new InvalidOperationException("Material flickered during repeated uploads: "+worst+"; uploads="+tick);
   checks.Add("dynamic upload stability: "+tick+" uploads, "+samples+" changed frames, maximum mean pixel drift "+worst.ToString("0.###"));
   await File.WriteAllLinesAsync(Path.Combine(output,"checks.txt"),checks);
  }finally{background.Close();island.Close();}
 }
 [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowPos")] private static extern bool Raise(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
}
