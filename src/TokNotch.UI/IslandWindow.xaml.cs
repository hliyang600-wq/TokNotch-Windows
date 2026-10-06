using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using TokNotch.UI.Themes;
using TokNotch.Core.Models;
using TokNotch.Core.Interaction;
using TokNotch.Infrastructure.Windows;
using TokNotch.UI.Animations;
using TokNotch.UI.Interaction;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;
public partial class IslandWindow : Window
{
 public static readonly DependencyProperty AnimationsEnabledProperty = DependencyProperty.Register(nameof(AnimationsEnabled), typeof(bool), typeof(IslandWindow), new PropertyMetadata(false));
 public bool AnimationsEnabled { get => (bool)GetValue(AnimationsEnabledProperty); private set => SetValue(AnimationsEnabledProperty, value); }
 internal bool? MotionPreferenceOverride { get; set; }
 private bool ReduceMotion => _window.Animation==AnimationMode.Off||(MotionPreferenceOverride??!SystemParameters.ClientAreaAnimation);
 private WindowPreferences _window=new();
 private MonitorWorkArea? _area;
 private readonly DispatcherTimer _follow=new(){Interval=TimeSpan.FromMilliseconds(400)};
 private bool _placing,_mouseDown,_dragging;
 private NativeMethods.Point _pointerStart;
 private NativeMethods.Rect _dragStart;
 public event Action<WindowPreferences>? PositionChanged;
 public event Action? SettingsRequested;
 public IslandStateController Policy { get; }
 public IslandMorphController Morph { get; }
 public NativeWindowService? Native { get; private set; }
 private readonly MonitorService _monitors = new();
 private readonly StaggerController _stagger;
 internal TokNotch.UI.Glass.GlassController? Glass { get; private set; }
 public IslandWindow(IslandViewModel model, bool enableGlass = true)
 {
  InitializeComponent(); DataContext = model;
  TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
  TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
  _stagger = new(HeaderRow, MetricsRow, ProviderRow);
  Policy = new(new DispatcherDeferredScheduler(Dispatcher)); Morph = new(ApplyMorph);
  Policy.TargetChanged += expanded => Morph.Animate(expanded, ReduceMotion,_window.Animation==AnimationMode.Reduced);
  Morph.Settled += Policy.AnimationSettled;
  MouseEnter += (_, _) => {if(!_mouseDown)Policy.PointerEnter();}; MouseLeave += (_, _) => {if(!_mouseDown)Policy.PointerLeave();};
  MouseLeftButtonDown += PointerDown;MouseMove+=PointerMove;MouseLeftButtonUp+=PointerUp;
  LostMouseCapture+=(_,_)=>{if(_mouseDown){_mouseDown=false;_dragging=false;ApplyMorph(Morph.ShapeProgress);Policy.PointerLeave();}};
  _follow.Tick+=(_,_)=>{if(!_mouseDown&&!Policy.TargetExpanded){var area=_monitors.Resolve(_window);if(area.Device!=_area?.Device){_area=area;ApplyMorph(Morph.ShapeProgress);}}};
  SourceInitialized += (_, _) => { var source = (HwndSource)PresentationSource.FromVisual(this); Native = new(source.Handle); Native.Configure(); source.AddHook(Hook); ApplyMorph(0); };
  Loaded += (_, _) => { if(enableGlass) Glass = new(Native!.Handle, Surface);ApplyPreferences(model.Preferences); };
  Closed += (_, _) => { _follow.Stop();Glass?.Dispose(); Morph.Dispose(); Policy.Dispose();PositionChanged=null; };
 }
 private void OpenSettings(object sender,RoutedEventArgs args){args.Handled=true;SettingsRequested?.Invoke();}
 private static bool WithinButton(DependencyObject? item){while(item!=null){if(item is Button)return true;item=item is System.Windows.Documents.Run run?run.Parent:VisualTreeHelper.GetParent(item);}return false;}
 private void SelectProvider(object sender, RoutedEventArgs args) { if (sender is Button button && button.Tag is Provider provider) ((IslandViewModel)DataContext).Select(provider); args.Handled = true; }
 private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
 {
  if (message == NativeMethods.WmMouseActivate) { handled = true; return new(NativeMethods.MaNoActivate); }
  if (message is NativeMethods.WmDisplayChange or NativeMethods.WmSettingChange or NativeMethods.WmDpiChanged) Dispatcher.BeginInvoke(()=>{if(message!=NativeMethods.WmDpiChanged)_area=null;RefreshMotionPolicy();ApplyTheme();});
  return IntPtr.Zero;
 }
 internal void RefreshMotionPolicy()
 {
  if (ReduceMotion) Morph.SetImmediate(Policy.TargetExpanded);
  else ApplyMorph(Morph.ShapeProgress);
 }
 public void ApplyPreferences(DisplayPreferences preferences)
 {
  var changed=_window.Animation!=preferences.Window.Animation;_window=preferences.Window;_area=_monitors.Resolve(_window);
  Policy.SetCollapseDelay(TimeSpan.FromMilliseconds(_window.CollapseDelayMilliseconds));Policy.SetMode(preferences.Expansion);
  if(ReduceMotion)Morph.SetImmediate(Policy.TargetExpanded);
  else if(changed&&Morph.IsRunning)Morph.Animate(Policy.TargetExpanded,false,_window.Animation==AnimationMode.Reduced);
  else ApplyMorph(Morph.ShapeProgress);
  if(_window.Display==DisplayTarget.FollowCursor)_follow.Start();else _follow.Stop();
  ApplyTheme();Glass?.Enable(preferences.GlassEnabled);
 }
 private void ApplyTheme(){ThemeManager.Apply(_window.Theme);Surface.ApplyTheme(ThemeManager.IsLight);((IslandViewModel)DataContext).RefreshTheme();UsageRing.InvalidateVisual();}
 private void PointerDown(object sender,MouseButtonEventArgs args)
 {
  if(WithinButton(args.OriginalSource as DependencyObject))return;
  if(!_window.DragEnabled){Policy.Click();return;}
  // Only the compact pill and expanded heading act as drag handles.
  var handle=WithinElement(args.OriginalSource as DependencyObject,Compact)||WithinElement(args.OriginalSource as DependencyObject,HeaderRow);
  if(!handle){Policy.Click();return;}
  if(!NativeMethods.GetCursorPos(out _pointerStart)||!NativeMethods.GetWindowRect(Native!.Handle,out _dragStart))return;
  _mouseDown=true;_dragging=false;Morph.SetImmediate(Policy.TargetExpanded);CaptureMouse();args.Handled=true;
 }
 private static bool WithinElement(DependencyObject? item,DependencyObject target)
 {
  while(item!=null){if(item==target)return true;item=item is System.Windows.Documents.Run run?run.Parent:VisualTreeHelper.GetParent(item);}return false;
 }
 private void PointerMove(object sender,MouseEventArgs args)
 {
  if(!_mouseDown||args.LeftButton!=MouseButtonState.Pressed||!NativeMethods.GetCursorPos(out var point))return;
  var dx=point.X-_pointerStart.X;var dy=point.Y-_pointerStart.Y;
  if(!_dragging&&Math.Abs(dx)+Math.Abs(dy)<6)return;
  _dragging=true;Native!.SetBounds(_dragStart.Left+dx,_dragStart.Top+dy,_dragStart.Right-_dragStart.Left,_dragStart.Bottom-_dragStart.Top);
 }
 private void PointerUp(object sender,MouseButtonEventArgs args)
 {
  if(!_mouseDown)return;_mouseDown=false;ReleaseMouseCapture();args.Handled=true;
  if(!_dragging){Policy.Click();Policy.PointerEnter();return;}
  _dragging=false;
  if(!NativeMethods.GetCursorPos(out var point)||!NativeMethods.GetWindowRect(Native!.Handle,out var rect))return;
  CompleteDrag(rect.Left,rect.Top,point.X,point.Y);
  if(NativeMethods.GetWindowRect(Native.Handle,out var settled))
  {
   var dpi=VisualTreeHelper.GetDpi(this);var local=new Point((point.X-settled.Left)/dpi.DpiScaleX,(point.Y-settled.Top)/dpi.DpiScaleY);
   if(Surface.Clip?.FillContains(local)==true)Policy.PointerEnter();else Policy.PointerLeave();
  }
 }
 internal void CompleteDrag(double x,double y,int cursorX,int cursorY)
 {
  var area=_monitors.AtPoint(cursorX,cursorY);var dpi=VisualTreeHelper.GetDpi(this);
  _window=DockLayout.FromDrag(area.Left,area.Top,area.Right,area.Bottom,x,y,IslandGeometry.HostWidth*dpi.DpiScaleX,IslandGeometry.HostHeight*dpi.DpiScaleY,_window,area.Device,dpi.DpiScaleX);
  _area=area;_follow.Stop();ApplyMorph(Morph.ShapeProgress);PositionChanged?.Invoke(_window);
 }
 private void ApplyMorph(double shapeProgress)
 {
  var progress=Math.Clamp(shapeProgress,0,1);
  var width = 180 + 200 * shapeProgress; var height = 32 + 188 * shapeProgress;
  var dpi = VisualTreeHelper.GetDpi(this);
  var area = _area??=_monitors.Resolve(_window);
  var position=DockLayout.Place(area.Left,area.Top,area.Right,area.Bottom,Math.Ceiling(IslandGeometry.HostWidth*dpi.DpiScaleX),Math.Ceiling(IslandGeometry.HostHeight*dpi.DpiScaleY),_window,dpi.DpiScaleX);
  // A fixed transparent host removes the asynchronous HWND resize/layout race.
  // Only the glass silhouette changes; every content element has a fixed desktop anchor.
  if(!_dragging&&!_placing)
  {
   _placing=true;
   try{Left=Math.Round(position.X)/dpi.DpiScaleX;Top=Math.Round(position.Y)/dpi.DpiScaleY;Width=IslandGeometry.HostWidth;Height=IslandGeometry.HostHeight;
    if(Native!=null&&NativeMethods.GetWindowRect(Native.Handle,out var rect)&&(rect.Left!=(int)Math.Round(position.X)||rect.Top!=(int)Math.Round(position.Y)||rect.Right-rect.Left!=(int)Math.Ceiling(IslandGeometry.HostWidth*dpi.DpiScaleX)||rect.Bottom-rect.Top!=(int)Math.Ceiling(IslandGeometry.HostHeight*dpi.DpiScaleY)))Native.SetBounds((int)Math.Round(position.X),(int)Math.Round(position.Y),(int)Math.Ceiling(IslandGeometry.HostWidth*dpi.DpiScaleX),(int)Math.Ceiling(IslandGeometry.HostHeight*dpi.DpiScaleY));}
   finally{_placing=false;}
  }
  Surface.SetShape(width,height,16+12*progress,_window.Edge);
  Canvas.SetLeft(Compact,_window.Edge==DockEdge.Left?0:_window.Edge==DockEdge.Right?200:100);
  Canvas.SetTop(Compact,_window.Edge==DockEdge.Bottom?188:_window.Edge==DockEdge.Top?0:94);
  Compact.Opacity = Math.Clamp(1 - progress * 3, 0, 1);
  Canvas.SetLeft(Expanded, 0); Expanded.Width = 340;
  Expanded.Opacity = 1; Expanded.IsHitTestVisible = progress > .95;
  AnimationsEnabled = !ReduceMotion && _window.Animation==AnimationMode.Normal && progress > .95;
  _stagger.Apply(progress, ReduceMotion);
 }
}

