namespace TokNotch.Core.Models;

public enum AppearanceTheme { Dark, Light, System }
public enum DockEdge { Top, Bottom, Left, Right }
public enum DisplayTarget { Primary, FollowCursor, Specific }
public enum AnimationMode { Normal, Reduced, Off }

/// <summary>Offset is a normalized position along an edge, independent of resolution and DPI.</summary>
public sealed record WindowPreferences(
 AppearanceTheme Theme=AppearanceTheme.Dark,
 DockEdge Edge=DockEdge.Top,
 DisplayTarget Display=DisplayTarget.Primary,
 string? MonitorDevice=null,
 double Offset=.5,
 int EdgeMargin=8,
 bool DragEnabled=true,
 AnimationMode Animation=AnimationMode.Normal,
 int CollapseDelayMilliseconds=300)
{
 public void Validate()
 {
  if(!Enum.IsDefined(Theme)||!Enum.IsDefined(Edge)||!Enum.IsDefined(Display)||!Enum.IsDefined(Animation))throw new ArgumentException("外观、位置或动画选项无效。");
  if(!double.IsFinite(Offset)||Offset<0||Offset>1)throw new ArgumentException("沿边位置需在 0–100% 之间。");
  if(EdgeMargin<0||EdgeMargin>100)throw new ArgumentException("边缘距离需在 0–100 之间。");
  if(CollapseDelayMilliseconds<200||CollapseDelayMilliseconds>1000)throw new ArgumentException("收起延迟需在 200–1000 毫秒之间。");
 }
}

public readonly record struct DockBounds(double X,double Y);
public static class DockLayout
{
 public static DockBounds Place(double left,double top,double right,double bottom,double width,double height,WindowPreferences preferences,double scale=1)
 {
  var margin=preferences.EdgeMargin*scale;
  margin=Math.Max(0,Math.Min(margin,Math.Min(Math.Max(0,right-left-width),Math.Max(0,bottom-top-height))/2));
  var x=left+margin+Math.Max(0,right-left-width-2*margin)*preferences.Offset;
  var y=top+margin+Math.Max(0,bottom-top-height-2*margin)*preferences.Offset;
  return preferences.Edge switch
  {
   DockEdge.Top=>new(x,top+margin),
   DockEdge.Bottom=>new(x,Math.Max(top,bottom-height-margin)),
   DockEdge.Left=>new(left+margin,y),
   _=>new(Math.Max(left,right-width-margin),y)
  };
 }
 public static WindowPreferences FromDrag(double left,double top,double right,double bottom,double x,double y,double width,double height,WindowPreferences previous,string device,double scale=1)
 {
  var centerX=x+width/2;var centerY=y+height/2;
  var distances=new[]{(DockEdge.Top,Math.Abs(centerY-top)),(DockEdge.Bottom,Math.Abs(bottom-centerY)),(DockEdge.Left,Math.Abs(centerX-left)),(DockEdge.Right,Math.Abs(right-centerX))};
  var edge=distances.MinBy(item=>item.Item2).Item1;
  var basePreference=previous with {Edge=edge,Display=DisplayTarget.Specific,MonitorDevice=device,Offset=0};
  var start=Place(left,top,right,bottom,width,height,basePreference,scale);
  var end=Place(left,top,right,bottom,width,height,basePreference with {Offset=1},scale);
  var denominator=edge is DockEdge.Top or DockEdge.Bottom?end.X-start.X:end.Y-start.Y;
  var numerator=edge is DockEdge.Top or DockEdge.Bottom?x-start.X:y-start.Y;
  return basePreference with {Offset=denominator<=0?.5:Math.Clamp(numerator/denominator,0,1)};
 }
}
