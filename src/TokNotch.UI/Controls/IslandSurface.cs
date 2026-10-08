using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using TokNotch.UI.Glass;
using TokNotch.Core.Models;
using TokNotch.UI.Animations;
using System.Windows.Media.Effects;
namespace TokNotch.UI.Controls;

[ContentProperty(nameof(Child))]
public sealed class IslandSurface : Grid
{
    private readonly ImageBrush backgroundImage = new() { Stretch=Stretch.Fill, ViewboxUnits=BrushMappingMode.RelativeToBoundingBox };
    private readonly Grid backdrop = new() { IsHitTestVisible=false,Visibility=Visibility.Collapsed,ClipToBounds=true,Width=IslandGeometry.HostWidth,Height=IslandGeometry.HostHeight };
    private readonly BlurEffect backgroundBlur = new() { KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Quality };
    private readonly Grid blurInput=new() { Width=IslandGeometry.HostWidth,Height=IslandGeometry.HostHeight,ClipToBounds=true };
    private Rect sourceBounds=new(0,0,IslandGeometry.HostWidth,IslandGeometry.HostHeight);
    internal int SamplingPadding { get; private set; }=55;
    private readonly RectangleGeometry roundedClip = new();
    private readonly Border rim = new() { BorderThickness=new Thickness(1), IsHitTestVisible=false };
    private readonly Border content = new() { Background=Brushes.Transparent,Width=IslandGeometry.ExpandedWidth,Height=IslandGeometry.ExpandedHeight };
    private readonly LiquidGlassEffect? glass;
    private WriteableBitmap? bitmap;
    private GlassMaterial material=new();
    private bool materialEnabled;
    private double baseWidth=180,baseHeight=32,baseRadius=16;
    private DockEdge dock;
    private readonly AnimatedScalar[] interaction;
    private Point pointer;
    private bool updatingInteraction,shapePending;
    public UIElement? Child { get=>content.Child; set=>content.Child=value; }
    public bool SupportsGlass => glass is not null;
    internal double MaterialWidth => roundedClip.Rect.Width;
    internal double MaterialHeight => roundedClip.Rect.Height;
    internal int BitmapAllocations { get; private set; }
    internal int ViewportUpdates { get; private set; }
    internal Rect BackdropViewport => new(roundedClip.Rect.X/IslandGeometry.HostWidth,roundedClip.Rect.Y/IslandGeometry.HostHeight,MaterialWidth/IslandGeometry.HostWidth,MaterialHeight/IslandGeometry.HostHeight);
    internal Point HighlightOffset => glass?.HighlightOffset??new();
    internal Rect CaptureBounds => sourceBounds;
    public IslandSurface()
    {
        interaction=Enumerable.Range(0,6).Select(_=>new AnimatedScalar(_=>PresentInteraction())).ToArray();
        for(var i=0;i<6;i++)interaction[i].Set(i is 2 or 3?1:0,false);
        Loaded+=(_,_)=>AnimationClock.Current.FrameCompleted+=FlushInteraction;
        Unloaded+=(_,_)=>{AnimationClock.Current.FrameCompleted-=FlushInteraction;shapePending=false;foreach(var tween in interaction)tween.Dispose();};
        Background = new LinearGradientBrush(Color.FromRgb(47,45,57),Color.FromRgb(22,21,29),90);
        rim.BorderBrush = new LinearGradientBrush(Color.FromArgb(120,255,255,255),Color.FromArgb(16,255,255,255),75);
        try { glass=new LiquidGlassEffect(); backdrop.Effect=glass; } catch { }
        Children.Add(backdrop); Children.Add(rim); Children.Add(content);
        blurInput.Children.Add(new System.Windows.Shapes.Rectangle { Fill=backgroundImage,Effect=backgroundBlur });backdrop.Children.Add(blurInput); Clip=roundedClip;
        content.Loaded+=(_,_)=>ApplyTextShadow(content);
        // Smooth coverage lives in a native mask, independent of each captured shader frame.
        OpacityMask=new DrawingBrush(new GeometryDrawing(Brushes.White,null,roundedClip)) { ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,IslandGeometry.HostWidth,IslandGeometry.HostHeight),ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,IslandGeometry.HostWidth,IslandGeometry.HostHeight),Stretch=Stretch.Fill };
        Width=IslandGeometry.HostWidth; Height=IslandGeometry.HostHeight;
        backdrop.HorizontalAlignment=rim.HorizontalAlignment=HorizontalAlignment.Center;
        backdrop.VerticalAlignment=rim.VerticalAlignment=VerticalAlignment.Top;
        SnapsToDevicePixels = true; UseLayoutRounding = true;
    }
    public void ApplyTheme(bool light)
    {
        Background=light?new SolidColorBrush(Color.FromRgb(234,237,244)):new LinearGradientBrush(Color.FromRgb(47,45,57),Color.FromRgb(22,21,29),90);
        rim.BorderBrush=light?new SolidColorBrush(Color.FromArgb(60,70,80,100)):new LinearGradientBrush(Color.FromArgb(120,255,255,255),Color.FromArgb(16,255,255,255),75);
        if(glass!=null)glass.Tint=light?Color.FromRgb(234,237,244):Color.FromRgb(19,18,24);
    }
    public void SetShape(double width, double height, double radius,DockEdge edge=DockEdge.Top)
    {
        baseWidth=width;baseHeight=height;baseRadius=radius;dock=edge;PresentShape();
    }
    internal void ConfigureMaterial(GlassMaterial settings,bool enabled)
    {
        material=settings;materialEnabled=enabled;glass?.Configure(settings);
        // CSS shadow blur-radius is twice sigma; WPF radius is three times sigma.
        Effect=enabled?new DropShadowEffect{Color=Colors.Black,Opacity=settings.OverLight?.75:.25,BlurRadius=settings.OverLight?105:60,ShadowDepth=settings.OverLight?16:12,Direction=270}:null;
        // WPF Gaussian sigma is Radius/3; CSS blur() specifies sigma directly.
        backgroundBlur.Radius=3*((settings.OverLight?12:4)+settings.Blur*32);
        SamplingPadding=(int)Math.Ceiling(backgroundBlur.Radius+settings.Displacement*(settings.OverLight?.25:.5)+2);
        content.Effect=null;ApplyTextShadow(content);
        rim.Opacity=enabled?0:1;PresentShape();
        backdrop.Visibility=enabled&&bitmap is not null?Visibility.Visible:Visibility.Collapsed;
    }
    private void ApplyTextShadow(DependencyObject element)
    {
        if(element is TextBlock text)text.Effect=materialEnabled&&!material.OverLight?TextShadow:null;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(element);i++)ApplyTextShadow(VisualTreeHelper.GetChild(element,i));
    }
    private static readonly DropShadowEffect TextShadow=MakeTextShadow();
    private static DropShadowEffect MakeTextShadow(){var effect=new DropShadowEffect{Color=Colors.Black,Opacity=.4,BlurRadius=18,ShadowDepth=2,Direction=270};effect.Freeze();return effect;}
    internal void SetInteraction(Point point,bool hovered,bool pressed,bool animate)
    {
        pointer=point;var target=material.Interaction(point.X,point.Y,baseWidth,baseHeight,pressed);
        var values=materialEnabled&&hovered&&animate?new[]{target.X,target.Y,target.ScaleX,target.ScaleY,1d,pressed?1d:0d}:new[]{0d,0d,1d,1d,hovered?1d:0d,0d};
        updatingInteraction=true;
        try{for(var i=0;i<values.Length;i++)interaction[i].Set(values[i],animate);}
        finally{updatingInteraction=false;PresentInteraction();}
    }
    private void PresentInteraction(){if(updatingInteraction)return;if(AnimationClock.Current.IsRendering)shapePending=true;else PresentShape();}
    private void FlushInteraction(){if(shapePending){shapePending=false;PresentShape();}}
    private void PresentShape()
    {
        if(interaction is null)return;
        var width=Math.Clamp(baseWidth*(interaction[2].Value??1),1,IslandGeometry.HostWidth);
        var height=Math.Clamp(baseHeight*(interaction[3].Value??1),1,IslandGeometry.HostHeight);
        var radius=materialEnabled?Math.Min(height/2,Math.Min(16,material.CornerRadius)+(material.CornerRadius-Math.Min(16,material.CornerRadius))*Math.Clamp((baseHeight-32)/188,0,1)):baseRadius;
        var edge=dock;
        var x=edge==DockEdge.Left?0:edge==DockEdge.Right?IslandGeometry.HostWidth-width:(IslandGeometry.HostWidth-width)/2;
        var y=edge==DockEdge.Top?0:edge==DockEdge.Bottom?IslandGeometry.HostHeight-height:(IslandGeometry.HostHeight-height)/2;
        x=Math.Clamp(x+(interaction[0].Value??0),0,IslandGeometry.HostWidth-width);y=Math.Clamp(y+(interaction[1].Value??0),0,IslandGeometry.HostHeight-height);
        content.HorizontalAlignment=edge==DockEdge.Left?HorizontalAlignment.Left:edge==DockEdge.Right?HorizontalAlignment.Right:HorizontalAlignment.Center;
        content.VerticalAlignment=edge==DockEdge.Top?VerticalAlignment.Top:edge==DockEdge.Bottom?VerticalAlignment.Bottom:VerticalAlignment.Center;
        backdrop.HorizontalAlignment=rim.HorizontalAlignment=HorizontalAlignment.Left;
        backdrop.VerticalAlignment=rim.VerticalAlignment=VerticalAlignment.Top;
        backdrop.Margin=new Thickness(sourceBounds.Left,sourceBounds.Top,0,0);rim.Margin=new Thickness(x,y,0,0);
        backdrop.Width=blurInput.Width=sourceBounds.Width;backdrop.Height=blurInput.Height=sourceBounds.Height;
        rim.Width=width; rim.Height=height; rim.CornerRadius = new CornerRadius(radius);
        roundedClip.Rect=new Rect(x,y,width,height); roundedClip.RadiusX=roundedClip.RadiusY=radius;
        // Blur and displace the complete host image; only clip after sampling outside the pill.
        backgroundImage.Viewbox=new Rect(0,0,1,1);
        ViewportUpdates++;
        if(glass is not null) { glass.Dimensions=new Point(width,height);glass.SourceDimensions=new Point(sourceBounds.Width,sourceBounds.Height);glass.Origin=new Point(x-sourceBounds.Left,y-sourceBounds.Top);glass.Radius=radius;glass.Interaction(new Point(pointer.X/width*100,pointer.Y/height*100),interaction[4].Value??0,interaction[5].Value??0); }
    }
    internal void SetBackdrop(byte[] pixels,int width,int height,Rect? bounds=null)
    {
        if(bounds is {} next&&next!=sourceBounds){sourceBounds=next;PresentShape();}
        if(bitmap is null||bitmap.PixelWidth!=width||bitmap.PixelHeight!=height) { bitmap=new WriteableBitmap(width,height,96,96,PixelFormats.Bgr32,null); backgroundImage.ImageSource=bitmap; BitmapAllocations++; }
        bitmap.WritePixels(new Int32Rect(0,0,width,height),pixels,width*4,0);
        backdrop.Visibility=materialEnabled?Visibility.Visible:Visibility.Collapsed;
    }
    internal void ClearBackdrop() { backgroundImage.ImageSource=null; bitmap=null;backdrop.Visibility=Visibility.Collapsed; }
}
