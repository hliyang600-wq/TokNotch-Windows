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
    private readonly System.Windows.Shapes.Rectangle backdrop = new() { IsHitTestVisible=false,Visibility=Visibility.Collapsed };
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
    public UIElement? Child { get=>content.Child; set=>content.Child=value; }
    public bool SupportsGlass => glass is not null;
    internal double MaterialWidth => backdrop.Width;
    internal double MaterialHeight => backdrop.Height;
    internal int BitmapAllocations { get; private set; }
    internal int ViewportUpdates { get; private set; }
    internal Rect BackdropViewport => backgroundImage.Viewbox;
    public IslandSurface()
    {
        interaction=Enumerable.Range(0,6).Select(_=>new AnimatedScalar(_=>PresentShape())).ToArray();
        for(var i=0;i<6;i++)interaction[i].Set(i is 2 or 3?1:0,false);
        Unloaded+=(_,_)=>{foreach(var tween in interaction)tween.Dispose();};
        Background = new LinearGradientBrush(Color.FromRgb(47,45,57),Color.FromRgb(22,21,29),90);
        rim.BorderBrush = new LinearGradientBrush(Color.FromArgb(120,255,255,255),Color.FromArgb(16,255,255,255),75);
        try { glass=new LiquidGlassEffect(); backdrop.Effect=glass; } catch { }
        Children.Add(backdrop); Children.Add(rim); Children.Add(content);
        backdrop.Fill=backgroundImage; Clip=roundedClip;
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
        Effect=enabled?new DropShadowEffect{Color=Colors.Black,Opacity=settings.OverLight?.75:.25,BlurRadius=settings.OverLight?70:40,ShadowDepth=settings.OverLight?16:12,Direction=270}:null;
        content.Effect=enabled&&!settings.OverLight?new DropShadowEffect{Color=Colors.Black,Opacity=.4,BlurRadius=12,ShadowDepth=2,Direction=270}:null;
        rim.Opacity=enabled?0:1;PresentShape();
        backdrop.Visibility=enabled&&bitmap is not null?Visibility.Visible:Visibility.Collapsed;
    }
    internal void SetInteraction(Point point,bool hovered,bool pressed,bool animate)
    {
        pointer=point;var target=material.Interaction(point.X,point.Y,baseWidth,baseHeight,pressed);
        var values=materialEnabled&&hovered&&animate?new[]{target.X,target.Y,target.ScaleX,target.ScaleY,1d,pressed?1d:0d}:new[]{0d,0d,1d,1d,hovered?1d:0d,0d};
        for(var i=0;i<values.Length;i++)interaction[i].Set(values[i],animate);
    }
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
        backdrop.Margin=rim.Margin=new Thickness(x,y,0,0);
        backdrop.Width=rim.Width=width; backdrop.Height=rim.Height=height; rim.CornerRadius = new CornerRadius(radius);
        roundedClip.Rect=new Rect(x,y,width,height); roundedClip.RadiusX=roundedClip.RadiusY=radius;
        // Crop the cached expanded rectangle in desktop coordinates, never stretch an old compact frame.
        backgroundImage.Viewbox=new Rect(x/IslandGeometry.HostWidth,y/IslandGeometry.HostHeight,width/IslandGeometry.HostWidth,height/IslandGeometry.HostHeight);
        ViewportUpdates++;
        if(glass is not null) { glass.Dimensions=new Point(width,height); glass.Radius=radius;glass.Interaction(pointer,interaction[4].Value??0,interaction[5].Value??0); }
    }
    internal void SetBackdrop(byte[] pixels,int width,int height)
    {
        if(bitmap is null||bitmap.PixelWidth!=width||bitmap.PixelHeight!=height) { bitmap=new WriteableBitmap(width,height,96,96,PixelFormats.Bgr32,null); backgroundImage.ImageSource=bitmap; BitmapAllocations++; }
        bitmap.WritePixels(new Int32Rect(0,0,width,height),pixels,width*4,0);
        backdrop.Visibility=materialEnabled?Visibility.Visible:Visibility.Collapsed;
    }
    internal void ClearBackdrop() { backgroundImage.ImageSource=null; bitmap=null;backdrop.Visibility=Visibility.Collapsed; }
}
