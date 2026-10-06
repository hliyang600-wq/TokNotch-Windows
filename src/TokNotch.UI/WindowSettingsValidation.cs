using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokNotch.Core.Models;
using TokNotch.Core.Interaction;
using TokNotch.Infrastructure.Windows;
using TokNotch.UI.Controls;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;

internal static class WindowSettingsValidation
{
 internal static async Task RunAsync(IslandWindow island,IslandViewModel model)
 {
  var output=ApplicationPaths.ArtifactsDirectory;var checks=new List<string>();
  void Check(bool okay,string name){if(!okay)throw new InvalidOperationException(name);checks.Add(name);}
  var original=model.Preferences;DisplayPreferences? saved=null;
  var settings=new SettingsWindow(original,p=>{saved=p;model.Configure(p);island.ApplyPreferences(p);},_=>Task.FromResult("fixture"),_=>Task.FromResult("fixture"));
  try
  {
   settings.Show();await Task.Delay(80);island.MotionPreferenceOverride=false;
   var appearance=(Button)settings.FindName("AppearanceTab");appearance.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(((FrameworkElement)settings.FindName("AppearancePage")).Visibility==Visibility.Visible&&((FrameworkElement)settings.FindName("DisplayPage")).Visibility==Visibility.Collapsed,"appearance has an independent settings page");
   var expandedRate=(ComboBox)settings.FindName("ExpandedRateBox");var collapsedRate=(ComboBox)settings.FindName("CollapsedRateBox");var customRate=(TextBox)settings.FindName("ExpandedCustomFps");
   Check(expandedRate.Items.Count==5&&collapsedRate.Items.Count==5,"both glass selectors expose exactly five requested choices");
   expandedRate.SelectedIndex=3;customRate.Text="47";collapsedRate.SelectedIndex=0;
   Check(customRate.Visibility==Visibility.Visible,"custom glass FPS input appears only for the custom choice");
   ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(saved?.Window.ExpandedRate.FramesPerSecond==47&&saved.Window.CollapsedRate.FramesPerSecond==15,"glass rate settings save independently");
   customRate.Text="0";((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(((TextBlock)settings.FindName("SaveStatus")).Text.Contains("1–360")&&saved?.Window.ExpandedRate.CustomFps==47,"invalid custom FPS is rejected without changing saved rates");
   expandedRate.SelectedIndex=4;Check(customRate.Visibility==Visibility.Collapsed,"display mode hides the custom FPS input");
   expandedRate.SelectedIndex=1;customRate.Text="30";collapsedRate.SelectedIndex=1;
   ((ComboBox)settings.FindName("ThemeBox")).SelectedIndex=1;((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(saved?.Window.Theme==AppearanceTheme.Light&&Themes.ThemeManager.IsLight,"saving light theme applies to the running island");
   Check(((SolidColorBrush)settings.FindResource("TextPrimary")).Color.R<80&&((SolidColorBrush)settings.Background).Color.R>200,"light theme provides dark text and a light settings surface");
   await Capture(settings,Path.Combine(output,"settings-appearance-light.png"));
   island.Policy.SetMode(ExpansionMode.AlwaysExpanded);await Idle();await Capture(island,Path.Combine(output,"island-light.png"));
   ((ComboBox)settings.FindName("ThemeBox")).SelectedIndex=0;
   ((Button)settings.FindName("PositionTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(((FrameworkElement)settings.FindName("PositionPage")).Visibility==Visibility.Visible&&((FrameworkElement)settings.FindName("AppearancePage")).Visibility==Visibility.Collapsed,"position replaces rather than stacks the previous page");
   var display=(ComboBox)settings.FindName("DisplayTargetBox");var monitors=(ComboBox)settings.FindName("MonitorBox");
   display.SelectedIndex=2;Check(monitors.IsEnabled&&monitors.Items.Count>0,"specific monitor mode enables available display selection");display.SelectedIndex=0;Check(!monitors.IsEnabled,"primary mode does not leave an active conflicting monitor selector");
   var edge=(ComboBox)settings.FindName("DockEdgeBox");var offset=(TextBox)settings.FindName("EdgeOffsetBox");var margin=(TextBox)settings.FindName("EdgeMarginBox");
   edge.SelectedIndex=1;offset.Text="30";margin.Text="12";
   ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);
   Check(saved?.Window.Edge==DockEdge.Bottom&&Math.Abs(saved.Window.Offset-.3)<.001&&saved.Window.EdgeMargin==12,"position editor saves dock, offset and margin");
   await Capture(settings,Path.Combine(output,"settings-position.png"));
   var area=new MonitorService().PrimaryWorkArea();var dpi=VisualTreeHelper.GetDpi(island);var handle=island.Native!.Handle;
   foreach(var side in Enum.GetValues<DockEdge>())
   {
    var p=original.WithWindow(new(Edge:side,Animation:AnimationMode.Off));model.Configure(p);island.ApplyPreferences(p);island.Policy.SetMode(ExpansionMode.Click);await Task.Delay(50);
    NativeMethods.GetWindowRect(handle,out var rect);var expected=DockLayout.Place(area.Left,area.Top,area.Right,area.Bottom,rect.Right-rect.Left,rect.Bottom-rect.Top,p.Window,dpi.DpiScaleX);
    Check(Math.Abs(rect.Left-expected.X)<=1&&Math.Abs(rect.Top-expected.Y)<=1,"native window placement matches "+side+" dock");
    var surface=(IslandSurface)island.FindName("Surface");var view=surface.BackdropViewport;
    Check(Math.Abs(view.Width-180d/IslandGeometry.HostWidth)<.0001&&Math.Abs(view.Height-32d/IslandGeometry.HostHeight)<.0001&&view.X>=0&&view.Y>=0&&view.Right<=1&&view.Bottom<=1,"collapsed glass crop stays anchored for "+side+" dock");
    island.Policy.Click();Check(island.Morph.Progress==1&&!island.Morph.IsRunning,"disabled animation snaps open on "+side+" dock");
   }
   var missing=original.WithWindow(new(Display:DisplayTarget.Specific,MonitorDevice:"missing-validation-monitor",Animation:AnimationMode.Off));island.ApplyPreferences(missing);await Task.Delay(40);
   NativeMethods.GetWindowRect(handle,out var fallback);Check(fallback.Left>=area.Left&&fallback.Right<=area.Right,"disconnected configured monitor falls back onto primary working area");
   WindowPreferences? dragged=null;void OnDrag(WindowPreferences value)=>dragged=value;island.PositionChanged+=OnDrag;
   try{island.CompleteDrag(area.Left+10,area.Top+200,area.Left+20,area.Top+220);}finally{island.PositionChanged-=OnDrag;}
   Check(dragged?.Edge==DockEdge.Left&&dragged.Display==DisplayTarget.Specific&&dragged.MonitorDevice==area.Device,"completed drag emits the snapped position for persistence");
   // Settings only update position when a drag is saved, preserving other unsaved fields.
   settings.UpdatePositionEditor(dragged!);Check(((ComboBox)settings.FindName("DockEdgeBox")).SelectedIndex==2&&((ComboBox)settings.FindName("ThemeBox")).SelectedIndex==0,"drag synchronizes the position editor without resetting its appearance draft");
   offset.Text="101";((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("SaveStatus")).Text.Contains("0–100"),"invalid position is reported without applying a different position");offset.Text="50";
   ((Button)settings.FindName("AnimationTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   ((ComboBox)settings.FindName("AnimationModeBox")).SelectedIndex=1;((ComboBox)settings.FindName("WindowModeBox")).SelectedIndex=1;((TextBox)settings.FindName("CollapseDelayBox")).Text="650";
   ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Idle();
   Check(saved?.Window.Animation==AnimationMode.Reduced&&island.Policy.CollapseDelay.TotalMilliseconds==650,"reduced animation and collapse delay reach the active window");
   island.Policy.Click();await Task.Delay(25);Check(island.Morph.IsRunning&&island.Morph.Progress is >0 and <1,"reduced motion performs a short transition rather than snapping");await Idle();Check(!island.AnimationsEnabled&&island.Morph.Progress==1,"reduced motion completes and disables numeric/ring interpolation");
   await Capture(settings,Path.Combine(output,"settings-animation.png"));
   ((TextBox)settings.FindName("CollapseDelayBox")).Text="100";((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("SaveStatus")).Text.Contains("200–1000"),"invalid collapse delay does not silently save");
   Check(island.Native.Handle==handle,"all position, theme and animation changes keep the same HWND");
  }
  finally{settings.Close();model.Configure(original);island.ApplyPreferences(original);island.MotionPreferenceOverride=null;await Idle();}
  await File.WriteAllLinesAsync(Path.Combine(output,"window-settings-checks.txt"),checks);
 }
 private static async Task Idle(){for(int i=0;i<100&&Animations.AnimationClock.Current.ActiveCount>0;i++)await Task.Delay(15);}
 private static async Task Capture(Window window,string file){window.UpdateLayout();await Task.Delay(60);var bitmap=new RenderTargetBitmap((int)(window.ActualWidth*1.5),(int)(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);}
}
