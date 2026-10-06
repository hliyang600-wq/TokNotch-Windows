using System.Text.Json;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Settings;
internal static class WindowPreferenceTests
{
 public static void Run(string root)
 {
  int count=0;void Check(bool condition,string text){if(!condition)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
  var defaults=DisplayPreferences.Default;
  Check(defaults.Window==new WindowPreferences(),"legacy window defaults remain dark, top centered, primary display and normal motion");
  var custom=new WindowPreferences(AppearanceTheme.Light,DockEdge.Right,DisplayTarget.Specific,"display-fixture",.27,20,false,AnimationMode.Reduced,650);
  var preferences=defaults.WithWindow(custom);var folder=Path.Combine(root,"window-preferences");var store=new DisplayPreferencesStore(folder);store.Save(preferences);
  Check(new DisplayPreferencesStore(folder).Load().Window==custom,"theme, dock, monitor, offset, drag and animation settings survive restart");
  Check(preferences.Providers.SequenceEqual(defaults.Providers)&&preferences.Rings[Provider.OpenAI]==defaults.Rings[Provider.OpenAI],"changing window settings retains sources and custom rings");
  var file=Path.Combine(folder,"config","display.json");using(var doc=JsonDocument.Parse(File.ReadAllText(file))){File.WriteAllText(file,JsonSerializer.Serialize(doc.RootElement.EnumerateObject().Where(pair=>pair.Name!="Window").ToDictionary(pair=>pair.Name,pair=>pair.Value)));}
  Check(store.Load().Window==new WindowPreferences(),"old settings without Window acquire safe defaults");
  bool Invalid(WindowPreferences value){try{value.Validate();return false;}catch(ArgumentException){return true;}}
  Check(Invalid(custom with{Offset=double.NaN})&&Invalid(custom with{Offset=1.1}),"non-finite and out-of-range positions rejected");
  Check(Invalid(custom with{Theme=(AppearanceTheme)99})&&Invalid(custom with{Edge=(DockEdge)99})&&Invalid(custom with{Animation=(AnimationMode)99}),"undefined appearance, dock and animation modes rejected");
  Check(Invalid(custom with{EdgeMargin=-1})&&Invalid(custom with{EdgeMargin=101})&&Invalid(custom with{CollapseDelayMilliseconds=199}),"invalid edge margins and collapse delay rejected");
  var top=DockLayout.Place(0,0,1920,1080,380,220,new());
  Check(top==new DockBounds(770,8),"primary top centered placement keeps original coordinates");
  Check(DockLayout.Place(0,0,1920,1080,380,220,new(Edge:DockEdge.Bottom))==new DockBounds(770,852),"bottom docking anchors expanded host above taskbar work boundary");
  Check(DockLayout.Place(-1920,-200,0,880,380,220,new(Edge:DockEdge.Left))==new DockBounds(-1912,230),"left docking supports negative monitor coordinates");
  Check(DockLayout.Place(0,0,1920,1080,380,220,new(Edge:DockEdge.Right,Offset:1))==new DockBounds(1532,852),"right docking and end offset stay in the working area");
  var scaled=DockLayout.Place(0,0,2880,1620,570,330,new(),1.5);
  Check(scaled==new DockBounds(1155,12),"DPI scale applies to edge margin and host size");
  var drag=DockLayout.FromDrag(0,0,1920,1080,12,420,380,220,custom,"new-device");
  Check(drag.Edge==DockEdge.Left&&drag.Display==DisplayTarget.Specific&&drag.MonitorDevice=="new-device"&&drag.Theme==custom.Theme&&drag.Animation==custom.Animation,"drag snaps to nearest edge and saves monitor while retaining appearance and animation");
  var snapped=DockLayout.Place(0,0,1920,1080,380,220,drag);
  Check(Math.Abs(snapped.Y-420)<.001&&snapped.X==20,"drag round trip preserves along-edge position");
  Check(DockLayout.FromDrag(0,0,1920,1080,400,-100,380,220,new(),"fixture").Edge==DockEdge.Top,"drag near upper boundary selects top edge");
  Console.WriteLine($"{count} appearance-position-animation data checks passed");
 }
}
