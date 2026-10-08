using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using TokNotch.Core.Models;
namespace TokNotch.UI.Themes;

internal static class ThemeManager
{
 public static bool IsLight {get;private set;}
 public static void Apply(AppearanceTheme theme)
 {
  bool light=theme==AppearanceTheme.Light;
  if(theme==AppearanceTheme.System)
  {
   try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");light=key?.GetValue("AppsUseLightTheme") is int value&&value!=0;}
   catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
  }
  IsLight=light;
  var palette=new Dictionary<string,(string Dark,string Light)>
  {
   ["TextPrimary"]=("#F4F3F1","#20232D"),["TextSecondary"]=("#AFB1BE","#555C6C"),["TextMuted"]=("#999FAF","#646C7C"),
   ["SettingsBackground"]=("#17181E","#F4F5F8"),["ControlBackground"]=("#242630","#FFFFFF"),["ControlBorder"]=("#3B3D49","#D2D7E0"),
   ["HoverBackground"]=("#373A46","#E2E7EE"),["SelectedBackground"]=("#364640","#D9EBE4"),["ActionBackground"]=("#36574B","#D6EBE2"),
   ["PositiveText"]=("#9DC7B6","#286A52"),["DividerBrush"]=("#323440","#D6DAE2"),["MetricCard"]=("#12FFFFFF","#70FFFFFF"),
   ["ProviderSelected"]=("#20FFFFFF","#16000000"),["RingTrack"]=("#22FFFFFF","#22000000")
  };
  foreach(var pair in palette){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?pair.Value.Light:pair.Value.Dark));brush.Freeze();Application.Current.Resources[pair.Key]=brush;}
 }
}
