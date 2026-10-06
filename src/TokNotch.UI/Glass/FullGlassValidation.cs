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
   island.Glass.Dispose(); // Freeze our authored backdrop, then capture the actual GPU-rendered island.
   using var capture=new DesktopBackdrop(island.Native!.Handle);
   int imageWidth=0;
   async Task<byte[]> Shot(string name,GlassMaterial material,bool isolateEdge=false){surface.ConfigureMaterial(material,true);if(isolateEdge)surface.Effect=null;await Task.Delay(300);var bytes=capture.Capture(island.Native.Handle,out var w,out var h)??throw new InvalidOperationException("GPU screenshot unavailable");imageWidth=w;var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgr32,null,bytes,w*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name+".png"));encoder.Save(file);return bytes.ToArray();}
   void Different(byte[] a,byte[] b,string label){double delta=0;for(int i=0;i<a.Length;i++)if(i%4!=3)delta+=Math.Abs(a[i]-b[i]);if(delta/a.Length<.03)throw new InvalidOperationException("Effect did not change actual GPU pixels: "+label);checks.Add(label);}
   var baseline=await Shot("standard",new());
   foreach(var mode in new[]{RefractionMode.Polar,RefractionMode.Prominent,RefractionMode.Shader})Different(baseline,await Shot(mode.ToString().ToLowerInvariant(),new(Mode:mode)),"GPU refraction "+mode);
   foreach(var item in new[]{("displacement",new GlassMaterial(Displacement:0)),("blur",new GlassMaterial(Blur:1)),("saturation",new GlassMaterial(Saturation:0)),("aberration",new GlassMaterial(Aberration:5)),("overlight",new GlassMaterial(OverLight:true)),("tint",new GlassMaterial(TintOpacity:.8)),("radius",new GlassMaterial(CornerRadius:0))})Different(baseline,await Shot(item.Item1,item.Item2),"GPU "+item.Item1);
   surface.ConfigureMaterial(new(),true);var anchor=((FrameworkElement)island.FindName("Expanded")).TranslatePoint(new(),island);
   surface.SetInteraction(new Point(150,25),true,false,true);await Task.Delay(400);var hover=await Shot("hover",new());Different(baseline,hover,"hover stretch and highlights");
   surface.SetInteraction(new Point(150,25),true,true,true);await Task.Delay(400);Different(hover,await Shot("press",new()),"press response");
   if(((FrameworkElement)island.FindName("Expanded")).TranslatePoint(new(),island)!=anchor)throw new InvalidOperationException("Text anchor moved with material.");checks.Add("text anchor stays fixed during material stretch");
   surface.SetInteraction(new(),false,false,true);await Task.Delay(450);if(Animations.AnimationClock.Current.ActiveCount!=0)throw new InvalidOperationException("Material animations remain active at idle.");checks.Add("material animation subscriptions stop at idle");
   if(surface.Background!=Brushes.Transparent||((FrameworkElement)surface.Children[0]).UseLayoutRounding)throw new InvalidOperationException("Opaque fill or layout rounding masks the shader edge coverage.");
   checks.Add("live material has no opaque underlay or rounded shader layout");
   surface.Child!.Visibility=Visibility.Hidden;surface.ApplyTheme(false);var canvas=(System.Windows.Controls.Canvas)background.Content;canvas.Children.Clear();canvas.Background=Brushes.White;
   var nativeScale=VisualTreeHelper.GetDpi(island).DpiScaleX;
   foreach(var scale in new[]{1d,1.25,1.5,2d}){
    var zoom=scale/nativeScale;surface.LayoutTransform=new ScaleTransform(zoom,zoom);island.Width=IslandGeometry.HostWidth*zoom;island.Height=IslandGeometry.HostHeight*zoom;background.Width=island.Width+100;background.Height=island.Height+100;
    var edgePixels=await Shot("edge-aa-"+(int)(scale*100),new(TintOpacity:1),true);var count=0;
    for(int y=0;y<Math.Ceiling(28*scale);y++)for(int x=(int)(12*scale);x<Math.Ceiling(40*scale);x++){
     var dx=(x+.5)/scale-40;var dy=(y+.5)/scale-28;var distance=(Math.Sqrt(dx*dx+dy*dy)-28)*scale;
     if(Math.Abs(distance)>.8)continue;var index=(y*imageWidth+x)*4;
     if(edgePixels[index+2]>30&&edgePixels[index+2]<245)count++;
    }
    if(count<5)throw new InvalidOperationException("No subpixel corner coverage at "+scale);checks.Add("GPU corner has subpixel coverage at "+(int)(scale*100)+"% ("+count+" pixels)");
   }
   await File.WriteAllLinesAsync(Path.Combine(output,"checks.txt"),checks);
  }finally{background.Close();island.Close();}
 }
 [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowPos")] private static extern bool Raise(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
}
