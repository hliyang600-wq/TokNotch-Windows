using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokNotch.Core.Models;
using TokNotch.Core.Interaction;
using TokNotch.UI.Controls;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;
internal static class SettingsValidation
{
 public static async Task RunAsync(IslandViewModel model)
 {
  var output=ApplicationPaths.ArtifactsDirectory;Directory.CreateDirectory(output);var checks=new List<string>();DisplayPreferences? saved=null;var refreshed=0;var shown=0;var exited=0;
  var progress=Path.Combine(output,"settings-progress.txt");File.WriteAllText(progress,$"start {DateTimeOffset.Now:HH:mm:ss}{Environment.NewLine}");void Mark(string step)=>File.AppendAllText(progress,step+Environment.NewLine);
  void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
  var settings=new SettingsWindow(DisplayPreferences.Default,p=>{saved=p;model.Configure(p);},_=>Task.FromResult("测试未联网"),_=>Task.FromResult("测试未联网"),()=>Task.FromResult("自动连接测试通过"),()=>Task.FromResult("断开测试通过"),key=>Task.FromResult(key=="fixture-key"?"DeepSeek 连接测试通过":"测试输入错误"),()=>Task.FromResult("清除 DeepSeek key 测试通过"),()=>Task.FromResult("清除 Kimi key 测试通过"),new SettingsCommands(()=>{refreshed++;return Task.FromResult("已刷新 20:30:00");},()=>shown++,()=>exited++));settings.Show();await Task.Delay(100);
  var slots=(StackPanel)settings.FindName("ProviderSlots");var first=(ComboBox)((Grid)slots.Children[0]).Children[1];first.SelectedValue=Provider.Mimo;
  Check(settings.Draft.Providers.SequenceEqual(new[]{Provider.Mimo,Provider.Dsh,Provider.Kimi}),"fourth source replaces visible source");settings.MoveProvider(0,1);Check(settings.Draft.Providers.SequenceEqual(new[]{Provider.Dsh,Provider.Mimo,Provider.Kimi}),"provider arrows reorder");
  first=(ComboBox)((Grid)slots.Children[0]).Children[1];first.SelectedValue=Provider.Kimi;Check(settings.Draft.Providers.SequenceEqual(new[]{Provider.Kimi,Provider.Mimo,Provider.Dsh}),"choosing duplicate swaps slots without duplicates");settings.MoveMetric(2,-1);settings.MoveMetric(1,-1);Check(settings.Draft.Metrics.SequenceEqual(new[]{UsageMetric.AllTime,UsageMetric.Today,UsageMetric.Month}),"metrics reorder independently");model.Configure(settings.Draft);model.Select(Provider.Dsh);
  Check(model.FirstMetricValue==model.Current!.Today.Tokens.Total&&model.SecondMetricValue==model.Current.Month.Tokens.Total&&model.ThirdMetricValue==(double?)model.Current.Balance,"balance remains last and daily/monthly order stays correct");
  Check(model.FirstProviderLabel=="Kimi"&&model.SecondProviderLabel=="小米 MiMo"&&model.ThirdProviderLabel=="DSH","visible button labels follow saved order");
  first=(ComboBox)((Grid)slots.Children[0]).Children[1];first.SelectedValue=Provider.DeepSeek;Check(settings.Draft.Providers[0]==Provider.DeepSeek,"fifth DeepSeek source selectable in settings");first.SelectedValue=Provider.Kimi;
  var ringProvider=(ComboBox)settings.FindName("RingProviderBox");var outerRing=(ComboBox)settings.FindName("OuterRingBox");var innerRing=(ComboBox)settings.FindName("InnerRingBox");var tokenScale=(TextBox)settings.FindName("RingTokenBaselineBox");
  ringProvider.SelectedValue=Provider.Dsh;outerRing.SelectedValue=RingContent.TodayTokens;innerRing.SelectedValue=RingContent.Balance;tokenScale.Text="500";
  Check(settings.Draft.RingFor(Provider.Dsh)==new RingChoice(RingContent.TodayTokens,RingContent.Balance,500),"settings selects independent outer and inner ring content with a token scale");
  ringProvider.SelectedValue=Provider.Kimi;Check(settings.Draft.RingFor(Provider.Kimi)==RingChoices.Default(Provider.Kimi),"switching ring source preserves another source's default");
  ringProvider.SelectedValue=Provider.Dsh;Check(outerRing.SelectedValue is RingContent.TodayTokens&&innerRing.SelectedValue is RingContent.Balance&&tokenScale.Text=="500","switching back restores the edited ring choices");
  innerRing.SelectedValue=RingContent.TodayTokens;((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("SaveStatus")).Text.Contains("不同指标"),"duplicate ring measures are rejected at save");innerRing.SelectedValue=RingContent.Balance;
  tokenScale.Text="0";((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("SaveStatus")).Text.Contains("Token 满圈"),"invalid token full-ring value is reported beside the save action");tokenScale.Text="500";
  ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(saved?.RingFor(Provider.Dsh)==new RingChoice(RingContent.TodayTokens,RingContent.Balance,500)&&model.Preferences.RingFor(Provider.Dsh)==saved.RingFor(Provider.Dsh),"saved ring choices apply to the island immediately");
  ((ScrollViewer)settings.FindName("SettingsScroll")).ScrollToVerticalOffset(400);await Capture(settings,Path.Combine(output,"settings-custom-rings.png"));settings.ShowDisplayPage();
  await Capture(settings,Path.Combine(output,"settings-display.png"));settings.ShowConnectionPage();await Task.Delay(50);((PasswordBox)settings.FindName("DeepSeekKey")).Password="fixture-key";((Button)settings.FindName("ConnectDeepSeekButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("DeepSeekStatus")).Text=="DeepSeek 连接测试通过"&&((PasswordBox)settings.FindName("DeepSeekKey")).Password=="","DeepSeek connection invokes API callback and clears password field");var auto=(Button)settings.FindName("AutoMimoButton");auto.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("MimoStatus")).Text=="自动连接测试通过","auto-login button invokes callback without manual Cookie");((Button)settings.FindName("DisconnectMimoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("MimoStatus")).Text=="断开测试通过","disconnect button invokes callback");
  ((Button)settings.FindName("ForgetDeepSeekButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("DeepSeekStatus")).Text=="清除 DeepSeek key 测试通过","forget button clears the stored DeepSeek key through its callback");
  ((Button)settings.FindName("ForgetKimiButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("KimiStatus")).Text=="清除 Kimi key 测试通过","forget button clears the stored Kimi key through its callback");await Capture(settings,Path.Combine(output,"settings-connections.png"));
  Check(((TextBox)settings.FindName("BaselineCny")).Text=="50"&&((TextBox)settings.FindName("BaselineUsd")).Text=="","default baselines are shown as ¥50 and an empty USD field");
  ((TextBox)settings.FindName("BaselineCny")).Text="120";((TextBox)settings.FindName("BaselineUsd")).Text="7.5";((TextBox)settings.FindName("RefreshSecondsBox")).Text="15";((TextBox)settings.FindName("BalanceSecondsBox")).Text="120";
  ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(saved is not null&&saved.AmountBaselineCny==120m&&saved.AmountBaselineUsd==7.5m&&saved.RefreshSeconds==15&&saved.BalanceRefreshSeconds==120,"save button carries both amount baselines and both refresh intervals into the applied preferences");
  Check(((TextBlock)settings.FindName("SaveStatus")).Text.Contains("已保存"),"successful save reports applied settings");
  Check(model.Preferences.AmountBaselineCny==120m&&model.Preferences.AmountBaselineUsd==7.5m&&model.Preferences.RefreshSeconds==15&&model.Preferences.BalanceRefreshSeconds==120,"saved baselines and intervals reach the live window immediately");
  ((TextBox)settings.FindName("BaselineCny")).Text="0";
  ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(saved!.AmountBaselineCny==120m&&((TextBlock)settings.FindName("SaveStatus")).Text.Contains("大于零"),"zero CNY baseline is rejected by the save button instead of silently saving");
  ((TextBox)settings.FindName("BaselineCny")).Text="50";((TextBox)settings.FindName("RefreshSecondsBox")).Text="1";
  ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(saved.RefreshSeconds==15&&((TextBlock)settings.FindName("SaveStatus")).Text.Contains("界面刷新间隔"),"an out-of-range refresh interval is rejected by the save button");
  Check(((TextBox)settings.FindName("RefreshSecondsBox")).Text=="1","a rejected interval is left in the box so the user can correct it");
  ((TextBox)settings.FindName("RefreshSecondsBox")).Text="30";((TextBox)settings.FindName("BalanceSecondsBox")).Text="300";((TextBox)settings.FindName("BaselineUsd")).Text="";
  Check(((ComboBox)settings.FindName("WindowModeBox")).SelectedIndex==0&&((ComboBox)settings.FindName("MaterialBox")).SelectedIndex==0,"window controls start from the saved defaults (hover, liquid glass)");
  ((ComboBox)settings.FindName("WindowModeBox")).SelectedIndex=1;((ComboBox)settings.FindName("MaterialBox")).SelectedIndex=1;
  ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(saved!.Expansion==ExpansionMode.Click&&!saved.GlassEnabled,"save button carries expansion mode and material into the applied preferences");
  Check(model.Preferences.Expansion==ExpansionMode.Click&&!model.Preferences.GlassEnabled,"saved window preferences reach the live window immediately");
  Check(settings.SelectedExpansion==ExpansionMode.Click&&!settings.SelectedGlass,"the tray choices are read back from the combo boxes");
  ((Button)settings.FindName("RefreshNowButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);
  Check(refreshed==1&&((TextBlock)settings.FindName("CommandStatus")).Text=="已刷新 20:30:00","the settings page refreshes data on demand");
  ((Button)settings.FindName("ShowIslandButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(shown==1&&((TextBlock)settings.FindName("CommandStatus")).Text.Contains("已展开"),"the settings page can open the island");
  ((Button)settings.FindName("ExitAppButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(exited==1,"the settings page can exit the program (confirmation stays in the host)");
  settings.ShowDisplayPage();await Task.Delay(60);settings.UpdateLayout();Mark("display page shown");
  var scroll=(SmoothScrollViewer)settings.FindName("SettingsScroll");
  Check(scroll.ScrollableHeight>0&&scroll.VerticalOffset==0,"the settings page opens at the top and is long enough to scroll");
  Mark($"scroll reachable height={scroll.ScrollableHeight:0} viewport={scroll.ViewportHeight:0} lines={SystemParameters.WheelScrollLines}");
  var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent};
  scroll.RaiseEvent(wheel);
  Mark($"wheel raised handled={wheel.Handled} gliding={scroll.IsGliding} offset={scroll.VerticalOffset:0.0}");
  Check(wheel.Handled&&scroll.VerticalOffset<0.5&&scroll.IsGliding,"one wheel notch starts a glide instead of snapping the page down");
  for(int i=0;i<40&&scroll.IsGliding;i++)await Task.Delay(25);
  Mark($"glide finished offset={scroll.VerticalOffset:0.0} gliding={scroll.IsGliding}");
  Check(!scroll.IsGliding&&scroll.VerticalOffset>0.5,"the glide runs to completion and leaves no rendering subscription behind");
  var resting=scroll.VerticalOffset;await Task.Delay(200);
  Check(Math.Abs(scroll.VerticalOffset-resting)<0.01,"the page comes to rest instead of creeping");
  var second=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent};
  scroll.RaiseEvent(second);
  for(int i=0;i<40&&scroll.IsGliding;i++)await Task.Delay(25);
  Check(scroll.VerticalOffset>resting,"a second notch continues from where the page came to rest");
  settings.ShowConnectionPage();await Task.Delay(60);
  Check(scroll.VerticalOffset==0,"switching pages returns to the top instead of keeping the old offset");
  settings.ShowDisplayPage();((ScrollViewer)settings.FindName("SettingsScroll")).ScrollToEnd();await Task.Delay(80);
  await Capture(settings,Path.Combine(output,"settings-rings.png"));settings.Close();await File.WriteAllLinesAsync(Path.Combine(output,"settings-ui-checks.txt"),checks);
 }
 private static async Task Capture(Window window,string file){window.UpdateLayout();await Task.Delay(50);var image=new RenderTargetBitmap((int)(window.ActualWidth*1.5),(int)(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(file);encoder.Save(stream);}
}

