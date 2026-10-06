using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using TokNotch.UI.Glass;
using TokNotch.Core.Models;
namespace TokNotch.UI.Controls;

[ContentProperty(nameof(Child))]
public sealed class IslandSurface : Grid
{
    private readonly ImageBrush backgroundImage = new() { Stretch=Stretch.Fill, ViewboxUnits=BrushMappingMode.RelativeToBoundingBox };
    private readonly System.Windows.Shapes.Rectangle backdrop = new() { IsHitTestVisible=false };
    private readonly RectangleGeometry roundedClip = new();
    private readonly Border rim = new() { BorderThickness=new Thickness(1), IsHitTestVisible=false };
    private readonly Border content = new() { Background=Brushes.Transparent,Width=IslandGeometry.ExpandedWidth,Height=IslandGeometry.ExpandedHeight };
    private readonly LiquidGlassEffect? glass;
    private WriteableBitmap? bitmap;
    public UIElement? Child { get=>content.Child; set=>content.Child=value; }
    public bool SupportsGlass => glass is not null;
    internal double MaterialWidth => backdrop.Width;
    internal double MaterialHeight => backdrop.Height;
    internal int BitmapAllocations { get; private set; }
    internal int ViewportUpdates { get; private set; }
    internal Rect BackdropViewport => backgroundImage.Viewbox;
    public IslandSurface()
    {
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
        var x=edge==DockEdge.Left?0:edge==DockEdge.Right?IslandGeometry.HostWidth-width:(IslandGeometry.HostWidth-width)/2;
        var y=edge==DockEdge.Top?0:edge==DockEdge.Bottom?IslandGeometry.HostHeight-height:(IslandGeometry.HostHeight-height)/2;
        content.HorizontalAlignment=edge==DockEdge.Left?HorizontalAlignment.Left:edge==DockEdge.Right?HorizontalAlignment.Right:HorizontalAlignment.Center;
        content.VerticalAlignment=edge==DockEdge.Top?VerticalAlignment.Top:edge==DockEdge.Bottom?VerticalAlignment.Bottom:VerticalAlignment.Center;
        backdrop.HorizontalAlignment=rim.HorizontalAlignment=edge==DockEdge.Left?HorizontalAlignment.Left:edge==DockEdge.Right?HorizontalAlignment.Right:HorizontalAlignment.Center;
        backdrop.VerticalAlignment=rim.VerticalAlignment=edge==DockEdge.Top?VerticalAlignment.Top:edge==DockEdge.Bottom?VerticalAlignment.Bottom:VerticalAlignment.Center;
        backdrop.Width=rim.Width=width; backdrop.Height=rim.Height=height; rim.CornerRadius = new CornerRadius(radius);
        roundedClip.Rect=new Rect(x,y,width,height); roundedClip.RadiusX=roundedClip.RadiusY=radius;
        // Crop the cached expanded rectangle in desktop coordinates, never stretch an old compact frame.
        backgroundImage.Viewbox=new Rect(x/IslandGeometry.HostWidth,y/IslandGeometry.HostHeight,width/IslandGeometry.HostWidth,height/IslandGeometry.HostHeight);
        ViewportUpdates++;
        if(glass is not null) { glass.Dimensions=new Point(width,height); glass.Radius=radius; }
    }
    internal void SetBackdrop(byte[] pixels,int width,int height)
    {
        if(bitmap is null||bitmap.PixelWidth!=width||bitmap.PixelHeight!=height) { bitmap=new WriteableBitmap(width,height,96,96,PixelFormats.Bgr32,null); backgroundImage.ImageSource=bitmap; BitmapAllocations++; }
        bitmap.WritePixels(new Int32Rect(0,0,width,height),pixels,width*4,0);
    }
    internal void ClearBackdrop() { backgroundImage.ImageSource=null; bitmap=null; }
}

