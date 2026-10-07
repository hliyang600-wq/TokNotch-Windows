using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokNotch.Core.Interaction;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Settings;
using TokNotch.Infrastructure.Windows;
using TokNotch.UI.Controls;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;

internal static class AppearancePreviewValidation
{
 internal static async Task RunAsync(IslandWindow island,IslandViewModel model)
 {
  var output=Path.Combine(ApplicationPaths.ArtifactsDirectory,"appearance-preview");Directory.CreateDirectory(output);
  var checks=new List<string>();void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);checks.Add(name);}
  var initial=DisplayPreferences.Default.WithWindow(new(Animation:AnimationMode.Off));model.Configure(initial);island.ApplyPreferences(initial);
  var store=new DisplayPreferencesStore(output);store.Save(initial);var file=Path.Combine(output,"config","display.json");var savedBytes=File.ReadAllBytes(file);
  var saves=0;var failSave=false;DisplayPreferences committed=initial;
  SettingsWindow Create()=>new(committed,p=>{if(failSave)throw new IOException("validation");store.Save(p);committed=p;saves++;model.Configure(p);island.ApplyPreferences(p);},_=>Task.FromResult("fixture"),_=>Task.FromResult("fixture"),previewIsland:island);
  static Rect Bounds(Window window){NativeMethods.GetWindowRect(new WindowInteropHelper(window).Handle,out var r);return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
  static void Click(SettingsWindow settings,string name)=>((Button)settings.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var surface=(IslandSurface)island.FindName("Surface");var settings=Create();
  try
  {
   settings.Show();await Task.Delay(150);var anchor=Bounds(island);
   // Deliberately overlap the island before opening the appearance page.
   var own=Bounds(settings);NativeMethods.SetWindowPos(new WindowInteropHelper(settings).Handle,IntPtr.Zero,(int)anchor.X,(int)anchor.Y,(int)own.Width,(int)own.Height,NativeMethods.SwpNoActivate|4);
   Click(settings,"AppearanceTab");await Task.Delay(100);
   Check(island.IsAppearancePreview&&island.Policy.Mode==ExpansionMode.AlwaysExpanded&&island.Morph.Progress==1,"appearance page opens and pins the original island expanded");
   Check(Bounds(island)==anchor,"opening preview keeps the native island anchor unchanged");
   Check(!Bounds(settings).IntersectsWith(anchor)&&new MonitorService().Displays.Any(a=>new Rect(a.Left,a.Top,a.Right-a.Left,a.Bottom-a.Top).Contains(Bounds(settings))),"settings move out of the island and stay within a monitor work area");
   var settingsAnchor=Bounds(settings);island.Policy.PointerLeave();await Task.Delay(450);
   Check(island.Policy.TargetExpanded&&island.Morph.Progress==1,"moving the pointer away cannot collapse the preview");
   ((TextBox)settings.FindName("EdgeOffsetBox")).Text="invalid"; // Unrelated draft validation must not block appearance.
   ((ComboBox)settings.FindName("ThemeBox")).SelectedIndex=1;
   ((Slider)settings.FindName("GlassCornerRadius")).Value=53;
   ((Slider)settings.FindName("GlassDisplacement")).Value=91;
   ((Slider)settings.FindName("GlassBlur")).Value=.27;
   Check(Themes.ThemeManager.IsLight&&((RectangleGeometry)surface.Clip).RadiusX==53,"theme and corner radius preview before confirmation");
   Check(surface.SamplingPadding==(int)Math.Ceiling(3*(4+.27*32)+91*.5+2),"refraction and blur preview without parsing another page's invalid draft");
   var custom=(TextBox)settings.FindName("ExpandedCustomFps");((ComboBox)settings.FindName("ExpandedRateBox")).SelectedIndex=3;custom.Text="47";
   Check(island.Glass!.TargetFrameRate==47,"custom frame rate applies to the expanded preview");
   custom.Text="";Check(((TextBlock)settings.FindName("SaveStatus")).Text.Contains("1–360")&&island.Glass.TargetFrameRate==47,"partial invalid input leaves the last valid preview running");
   Click(settings,"PositionTab");Click(settings,"AppearanceTab");
   Check(island.IsAppearancePreview&&island.Policy.TargetExpanded,"returning with unfinished input still opens and pins the island");custom.Text="47";
   Check(saves==0&&ReferenceEquals(model.Preferences,initial)&&File.ReadAllBytes(file).SequenceEqual(savedBytes),"live preview never changes the model preferences or persisted bytes");
   Check(Bounds(island)==anchor&&Bounds(settings)==settingsAnchor,"sliders keep both windows in place");
   Click(settings,"PositionTab");await Task.Delay(50);
   Check(!island.IsAppearancePreview&&!Themes.ThemeManager.IsLight&&island.Policy.Mode==ExpansionMode.Hover,"leaving appearance restores the confirmed theme and expansion policy");
   Click(settings,"AppearanceTab");await Task.Delay(50);
   Check(Themes.ThemeManager.IsLight&&((Slider)settings.FindName("GlassCornerRadius")).Value==53&&((RectangleGeometry)surface.Clip).RadiusX==53,"returning to appearance reapplies the retained draft");
   ((TextBox)settings.FindName("EdgeOffsetBox")).Text="50";
   ((ComboBox)settings.FindName("MaterialBox")).SelectedIndex=1;
   Check(!island.Glass.CaptureActive,"frosted preview suspends capture immediately");
   ((ComboBox)settings.FindName("MaterialBox")).SelectedIndex=0;
   Check(island.Glass.CaptureActive,"liquid glass preview resumes capture immediately");
   failSave=true;Click(settings,"SavePreferencesButton");
   Check(island.IsAppearancePreview&&saves==0&&File.ReadAllBytes(file).SequenceEqual(savedBytes)&&((TextBlock)settings.FindName("SaveStatus")).Text.Contains("保存失败"),"failed confirmation keeps the preview and original file intact");
   failSave=false;((Slider)settings.FindName("GlassSaturation")).Value=169;settings.UpdateLayout();await Capture(settings,Path.Combine(output,"settings-live-preview.png"));
   settings.Close();await Task.Delay(80);
   Check(!island.IsAppearancePreview&&!Themes.ThemeManager.IsLight&&saves==0&&File.ReadAllBytes(file).SequenceEqual(savedBytes),"closing without confirmation rolls back appearance without saving");
   Check(Bounds(island)==anchor&&island.Policy.Mode==ExpansionMode.Hover,"cancel restores the dock anchor and hover behavior");
   settings=Create();settings.Show();Click(settings,"AppearanceTab");
   ((ComboBox)settings.FindName("ThemeBox")).SelectedIndex=1;((Slider)settings.FindName("GlassCornerRadius")).Value=41;
   Click(settings,"SavePreferencesButton");await Task.Delay(60);
   Check(saves==1&&store.Load().Window.Glass.CornerRadius==41&&store.Load().Window.Theme==AppearanceTheme.Light&&!island.IsAppearancePreview,"confirmation commits the appearance and ends preview");
   ((Slider)settings.FindName("GlassCornerRadius")).Value=67;((ComboBox)settings.FindName("ThemeBox")).SelectedIndex=0;
   Check(island.IsAppearancePreview&&!Themes.ThemeManager.IsLight&&store.Load().Window.Glass.CornerRadius==41,"editing after confirmation starts a new unsaved preview");
   settings.Close();await Task.Delay(60);
   Check(Themes.ThemeManager.IsLight&&((RectangleGeometry)surface.Clip).RadiusX<=41&&store.Load().Window.Glass.CornerRadius==41,"closing rolls back to the most recent confirmation");
   // Click mode remembers its prior expanded state across preview, including timer cancellation.
   committed=new(initial.Providers,initial.Metrics,expansion:ExpansionMode.Click,window:initial.Window);model.Configure(committed);island.ApplyPreferences(committed);island.Policy.Click();
   settings=Create();settings.Show();Click(settings,"AppearanceTab");settings.Close();
   Check(island.Policy.Mode==ExpansionMode.Click&&island.Policy.TargetExpanded,"cancel restores a previously expanded click-mode island");
   island.Policy.Click();settings=Create();settings.Show();Click(settings,"AppearanceTab");settings.Close();
   Check(island.Policy.Mode==ExpansionMode.Click&&!island.Policy.TargetExpanded,"cancel restores a previously collapsed click-mode island");
   foreach(var edge in Enum.GetValues<DockEdge>())
   {
    committed=initial.WithWindow(initial.Window with {Edge=edge});model.Configure(committed);island.ApplyPreferences(committed);var before=Bounds(island);
    settings=Create();settings.Show();Click(settings,"AppearanceTab");await Task.Delay(30);
    Check(Bounds(island)==before&&!Bounds(settings).IntersectsWith(before),"settings avoid the unchanged "+edge+" dock");settings.Close();
   }
   foreach(var scale in new[]{1d,1.5,2})
   {
    var area=new Rect(-1920*scale,0,1920*scale,1080*scale);var panel=new Rect(-1162*scale,8*scale,404*scale,240*scale);var current=new Rect(-1100*scale,160*scale,620*scale,660*scale);
    var placed=SettingsWindow.PlaceBesidePanel(current,panel,new[]{area},16*scale);
    Check(area.Contains(placed)&&!placed.IntersectsWith(panel),"placement handles negative monitor origins and "+scale+"x DPI");
    Check(SettingsWindow.PlaceBesidePanel(placed,panel,new[]{area},16*scale)==placed,"placement stays stable at "+scale+"x DPI");
   }
   var narrow=new Rect(0,0,1024,768);var blocked=new Rect(310,8,404,240);var other=new Rect(-1920,0,1920,1080);var moved=SettingsWindow.PlaceBesidePanel(new(300,40,620,660),blocked,new[]{narrow,other},16);
   Check(other.Contains(moved)&&!moved.IntersectsWith(blocked),"another monitor is used when the local work area cannot fit both windows");
   Check(island.Glass.Status.Contains("Desktop")||island.Glass.Readbacks>0,"real desktop capture survives live material switching");
   await File.WriteAllLinesAsync(Path.Combine(output,"checks.txt"),checks);
  }
  finally{settings.Close();model.Configure(initial);island.ApplyPreferences(initial);}
 }
 private static async Task Capture(Window window,string file)
 {
  window.UpdateLayout();await Task.Delay(60);var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);
 }
}
