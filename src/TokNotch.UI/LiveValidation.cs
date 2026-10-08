using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokNotch.UI.ViewModels;
using TokNotch.UI.Controls;
using TokNotch.Core.Models;
namespace TokNotch.UI;
internal static class LiveValidation
{
 internal static async Task RunAsync(IslandWindow window,IslandViewModel model)
 {
  var output=ApplicationPaths.ArtifactsDirectory;Directory.CreateDirectory(output);
  window.MotionPreferenceOverride=true;window.Policy.SetMode(TokNotch.Core.Interaction.ExpansionMode.AlwaysExpanded);window.RefreshMotionPolicy();
  var checks=new List<string>();
  foreach(var provider in new[]{Provider.OpenAI,Provider.Dsh,Provider.Kimi,Provider.Mimo,Provider.DeepSeek}){
   model.Select(provider);await Task.Delay(100);window.UpdateLayout();
   if(model.Current is null||!model.Live)throw new Exception("Missing live provider");
   var today=(AnimatedNumber)window.FindName("TodayNumber");var month=(AnimatedNumber)window.FindName("MonthNumber");
   if(provider is Provider.OpenAI or Provider.Dsh && (today.DisplayedValue!=model.Current.Today.Tokens.Total||month.DisplayedValue!=model.Current.Month.Tokens.Total))throw new Exception("Live number binding mismatch");
   if(provider is Provider.Kimi or Provider.Mimo && (today.Text!="—"||model.PaybackValue!=null))throw new Exception("Unconfigured Kimi must remain unknown");
   if(provider==Provider.DeepSeek&&(((AnimatedNumber)window.FindName("TokensNumber")).Text!="—"||model.ThirdMetricLabel!="可用余额"))throw new Exception("Unconfigured DeepSeek balance must remain unknown");
   var bitmap=new RenderTargetBitmap((int)(window.ActualWidth*1.5),(int)(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,"live-"+provider+".png"));png.Save(stream);checks.Add(provider+": live metric bindings correct");
  }
  await File.WriteAllLinesAsync(Path.Combine(output,"live-ui-checks.txt"),checks);
 }
}
