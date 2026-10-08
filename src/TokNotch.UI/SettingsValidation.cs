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
 public static async Task RunRowsAsync(IslandWindow island,IslandViewModel model)
 {
  var output=Path.Combine(ApplicationPaths.ArtifactsDirectory,"platform-rows");Directory.CreateDirectory(output);var checks=new List<string>();
  void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
  var now=DateTimeOffset.Now;
  PeriodUsage Usage(long tokens)=>new(new(tokens,0),null,CostStatus.Unavailable);
  var vendors=ProviderCatalog.Available.Select((p,index)=>new ProviderUsageSnapshot(p.Id,p.Name,p.Name,p.Name,DetectionState.Ready,Usage(250+index),Usage(500+index),Usage(1000+index),Usage(8000+index),null,new("fixture."+p.Name,0.72,"五小时额度"){RecordedAt=now,ResetsAt=now.AddDays(20)}){InnerRing=new("fixture.week",0.85,"一周额度"){RecordedAt=now},Balance=23.45m,Cash=20m,Voucher=3.45m,Currency="CNY",BalanceSource=p.Id==Provider.Dsh?"DeepSeek":p.Name,TokenUsageAvailable=p.Id!=Provider.Kimi}).ToArray();
  UsageSnapshot Snapshot(IEnumerable<ProviderUsageSnapshot> entries)=>new(now,now,TimeZoneInfo.Local.Id,false,RefreshState.Ready,entries,Array.Empty<ModelUsage>());
  var initial=new DisplayPreferences(DisplayPreferences.Default.Providers,new[]{UsageMetric.AllTime,UsageMetric.Today,UsageMetric.Month},expansion:ExpansionMode.AlwaysExpanded,glassEnabled:false,window:new(Animation:AnimationMode.Off));
  model.Configure(initial);model.Apply(Snapshot(vendors));island.ApplyPreferences(initial);island.Policy.SetMode(ExpansionMode.AlwaysExpanded);await Task.Delay(60);
  var number=(AnimatedNumber)island.FindName("TodayNumber");
  foreach(var provider in ProviderCatalog.Available)
  {
   model.Select(provider.Id);var data=vendors.Single(v=>v.Provider==provider.Id);
   foreach(var metric in DisplayRows.Available(provider.Id))
   {
    var choice=new[]{metric}.Concat(DisplayRows.Available(provider.Id).Where(m=>m!=metric).Take(2)).ToArray();
    var rows=initial.Rows.ToDictionary(p=>p.Key,p=>p.Value);rows[provider.Id]=choice;model.Configure(new(initial.Providers,initial.Metrics,window:initial.Window,rows:rows));model.Select(provider.Id);await Task.Delay(15);
    double? expected=metric switch{RingContent.TodayTokens=>data.Today.Tokens.Total,RingContent.MonthTokens=>data.Month.Tokens.Total,RingContent.AllTimeTokens=>data.AllTime.Tokens.Total,RingContent.Balance=>(double?)data.Balance,RingContent.CashBalance=>(double?)data.Cash,RingContent.GiftBalance=>(double?)data.Voucher,RingContent.FiveHourRemaining=>0.72,RingContent.WeeklyRemaining=>0.85,RingContent.MonthlyRemaining=>0.72,RingContent.MonthlyUsed=>1-.72,RingContent.QuotaResetTime=>data.Ring.ResetsAt?.ToUnixTimeSeconds(),RingContent.TodayOfMonth=>(double)data.Today.Tokens.Total/data.Month.Tokens.Total,_=>null};
    Check(number.Number==expected,provider.Name+" selected row reads "+metric+" from its own snapshot");
    if(metric==RingContent.QuotaResetTime)Check(number.Text==data.Ring.ResetsAt!.Value.LocalDateTime.ToString("MM-dd HH:mm")&&!number.IsAnimating,"reset time shows a local date and never animates through intermediate timestamps");
    if(metric==RingContent.Balance)Check(number.Text=="¥23.45",provider.Name+" money row uses currency units in any position");
    if(metric==RingContent.FiveHourRemaining)Check(number.Text=="72%","Codex five-hour row displays a percentage");
    if(metric==RingContent.WeeklyRemaining)Check(number.Text=="85%","Codex weekly row displays a percentage");
   }
  }
  model.Configure(initial);model.Select(Provider.Dsh);
  var store=new TokNotch.Infrastructure.Settings.DisplayPreferencesStore(output);store.Save(initial);DisplayPreferences committed=initial;int saves=0;
  SettingsWindow Create()=>new(committed,p=>{store.Save(p);committed=p;saves++;model.Configure(p);island.ApplyPreferences(p);},_=>Task.FromResult("fixture"),_=>Task.FromResult("fixture"),previewIsland:island,loginQwen:()=>Task.FromResult("千问登录测试通过"),connectQwen:cookie=>Task.FromResult(cookie=="fixture-cookie"?"千问连接测试通过":"测试输入错误"),forgetQwen:()=>Task.FromResult("千问清除测试通过"));
  var settings=Create();settings.Left=island.Left;settings.Top=island.Top+250;settings.Show();await Task.Delay(60);
  settings.ShowConnectionPage();((Button)settings.FindName("LoginQwenButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("QwenStatus")).Text=="千问登录测试通过","Qwen login button invokes the native login callback");
  ((PasswordBox)settings.FindName("QwenCookie")).Password="fixture-cookie";((Button)settings.FindName("ConnectQwenButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("QwenStatus")).Text=="千问连接测试通过"&&((PasswordBox)settings.FindName("QwenCookie")).Password=="","Qwen manual connection invokes its own callback and clears the password field");
  ((Button)settings.FindName("ForgetQwenButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(((TextBlock)settings.FindName("QwenStatus")).Text=="千问清除测试通过","Qwen disconnect invokes only its own credential callback");
  await Capture(settings,Path.Combine(output,"qwen-connections.png"));((Button)settings.FindName("DisplayTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var providerBox=(ComboBox)settings.FindName("MetricProviderBox");var slots=(StackPanel)settings.FindName("MetricSlots");
  ComboBox Row(int index)=>(ComboBox)((Grid)slots.Children[index]).Children[1];
  providerBox.SelectedValue=Provider.Dsh;Row(0).SelectedValue=RingContent.Balance;
  Check(settings.Draft.RowsFor(Provider.Dsh).SequenceEqual(new[]{RingContent.Balance,RingContent.MonthTokens,RingContent.TodayTokens}),"selecting an existing metric swaps rows without duplicates");
  Row(1).SelectedValue=RingContent.AllTimeTokens;Row(2).SelectedValue=RingContent.MonthTokens;settings.MoveMetric(2,-1);
  var dshRows=new[]{RingContent.Balance,RingContent.MonthTokens,RingContent.AllTimeTokens};
  Check(settings.Draft.RowsFor(Provider.Dsh).SequenceEqual(dshRows),"DSH can show balance first plus monthly and cumulative usage");
  providerBox.SelectedValue=Provider.OpenAI;Row(0).SelectedValue=RingContent.WeeklyRemaining;Row(1).SelectedValue=RingContent.FiveHourRemaining;
  Check(settings.Draft.RowsFor(Provider.Dsh).SequenceEqual(dshRows),"switching the edited platform preserves DSH draft rows");
  providerBox.SelectedValue=Provider.Kimi;Check(Row(0).Items.Count==4,"Kimi offers its three supported money fields and a hidden-row option");Row(0).SelectedValue=RingContent.None;Row(1).SelectedValue=RingContent.None;Check(settings.Draft.RowsFor(Provider.Kimi).Count(m=>m==RingContent.None)==2,"multiple rows can be hidden without swapping another hidden row");Row(0).SelectedValue=RingContent.Balance;Row(1).SelectedValue=RingContent.GiftBalance;Row(2).SelectedValue=RingContent.CashBalance;
  providerBox.SelectedValue=Provider.DeepSeek;Row(0).SelectedValue=RingContent.CashBalance;Check(settings.Draft.RowsFor(Provider.DeepSeek)[0]==RingContent.CashBalance,"hidden DeepSeek platform can select its cash balance independently");
  providerBox.SelectedValue=Provider.Dsh;
  Check(model.FirstMetricLabel=="今日"&&saves==0,"editing settings changes a draft, not the active island");
  ((Button)settings.FindName("SavePreferencesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);
  Check(saves==1&&model.FirstMetricLabel=="DeepSeek 余额"&&number.Text=="¥23.45"&&model.ThirdMetricValue==8001,"confirmation applies all three selected rows immediately");
  Check(store.Load().RowsFor(Provider.Dsh).SequenceEqual(dshRows),"confirmed row order survives settings reload");
  await Capture(settings,Path.Combine(output,"settings-platform-rows.png"));await Capture(island,Path.Combine(output,"dsh-custom-rows.png"));
  foreach(var provider in ProviderCatalog.Available){model.Select(provider.Id);await Task.Delay(30);Check((!model.FirstMetricVisible||model.FirstMetricLabel.Length>0)&&(!model.ThirdMetricVisible||model.ThirdMetricLabel.Length>0),provider.Name+" renders the saved independent layout");await Capture(island,Path.Combine(output,provider.Id+"-custom-rows.png"));}
  model.Select(Provider.Dsh);model.Apply(Snapshot(vendors.Select(v=>v.Provider==Provider.Dsh?v with{Balance=null,TokenUsageAvailable=false}:v)));await Task.Delay(25);Check(number.Text=="—"&&model.SecondMetricValue==null&&model.ThirdMetricValue==null,"missing balances and missing token history stay unknown, not zero");
  model.Apply(Snapshot(vendors.Select(v=>v.Provider==Provider.Dsh?v with{Balance=0}:v)));await Task.Delay(25);Check(number.Text=="¥0.00","known zero balance is distinct from missing data");
  model.Apply(Snapshot(vendors.Select(v=>v.Provider==Provider.Dsh?v with{Balance=-1.25m,Currency="USD"}:v)));await Task.Delay(25);Check(number.Text=="-$1.25","custom first row preserves USD and negative balances");
  model.Apply(Snapshot(vendors));settings.Close();var savedBytes=File.ReadAllBytes(Path.Combine(output,"config","display.json"));
  settings=Create();settings.Show();await Task.Delay(30);providerBox=(ComboBox)settings.FindName("MetricProviderBox");slots=(StackPanel)settings.FindName("MetricSlots");providerBox.SelectedValue=Provider.Dsh;Row(0).SelectedValue=RingContent.TodayTokens;settings.Close();
  Check(saves==1&&model.FirstMetricLabel=="DeepSeek 余额"&&savedBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(output,"config","display.json"))),"closing without confirmation discards row edits and leaves disk and island unchanged");
  Check(committed.WithWindow(committed.Window with{Theme=AppearanceTheme.Light}).RowsFor(Provider.Dsh).SequenceEqual(dshRows),"appearance updates retain per-platform rows");
  var hidden=committed.Rows.ToDictionary(p=>p.Key,p=>p.Value);hidden[Provider.Kimi]=new[]{RingContent.Balance,RingContent.None,RingContent.None};model.Configure(new(committed.Providers,committed.Metrics,window:committed.Window,rows:hidden));model.Select(Provider.Kimi);await Task.Delay(25);island.UpdateLayout();
  Check(((Grid)island.FindName("SecondMetricRow")).Visibility==Visibility.Collapsed&&((Grid)island.FindName("ThirdMetricRow")).Visibility==Visibility.Collapsed,"hidden rows collapse in the actual island");
  var card=(Border)island.FindName("MetricCard");var layout=(Grid)card.Child;Check(layout.RowDefinitions[1].ActualHeight==0&&layout.RowDefinitions[2].ActualHeight==0,"hidden rows release their layout space");await Capture(island,Path.Combine(output,"kimi-one-row.png"));
  hidden[Provider.Kimi]=new[]{RingContent.None,RingContent.None,RingContent.None};model.Configure(new(committed.Providers,committed.Metrics,window:committed.Window,rows:hidden));model.Select(Provider.Kimi);await Task.Delay(25);Check(card.Visibility==Visibility.Collapsed,"hiding all rows removes the empty metric card");
  await File.WriteAllLinesAsync(Path.Combine(output,"checks.txt"),checks.Select(c=>"PASS "+c).Append($"{checks.Count} platform row checks passed"));
 }
 public static async Task RunAsync(IslandViewModel model)
 {
  var output=ApplicationPaths.ArtifactsDirectory;Directory.CreateDirectory(output);var checks=new List<string>();DisplayPreferences? saved=null;var refreshed=0;var shown=0;var exited=0;
  var progress=Path.Combine(output,"settings-progress.txt");File.WriteAllText(progress,$"start {DateTimeOffset.Now:HH:mm:ss}{Environment.NewLine}");void Mark(string step)=>File.AppendAllText(progress,step+Environment.NewLine);
  void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
  var settings=new SettingsWindow(DisplayPreferences.Default,p=>{saved=p;model.Configure(p);},_=>Task.FromResult("测试未联网"),_=>Task.FromResult("测试未联网"),()=>Task.FromResult("自动连接测试通过"),()=>Task.FromResult("断开测试通过"),key=>Task.FromResult(key=="fixture-key"?"DeepSeek 连接测试通过":"测试输入错误"),()=>Task.FromResult("清除 DeepSeek key 测试通过"),()=>Task.FromResult("清除 Kimi key 测试通过"),new SettingsCommands(()=>{refreshed++;return Task.FromResult("已刷新 20:30:00");},()=>shown++,()=>exited++));settings.Show();await Task.Delay(100);
  var slots=(StackPanel)settings.FindName("ProviderSlots");var first=(ComboBox)((Grid)slots.Children[0]).Children[1];first.SelectedValue=Provider.Mimo;
  Check(settings.Draft.Providers.SequenceEqual(new[]{Provider.Mimo,Provider.Dsh,Provider.Kimi}),"fourth source replaces visible source");settings.MoveProvider(0,1);Check(settings.Draft.Providers.SequenceEqual(new[]{Provider.Dsh,Provider.Mimo,Provider.Kimi}),"provider arrows reorder");
  first=(ComboBox)((Grid)slots.Children[0]).Children[1];first.SelectedValue=Provider.Kimi;Check(settings.Draft.Providers.SequenceEqual(new[]{Provider.Kimi,Provider.Mimo,Provider.Dsh}),"choosing duplicate swaps slots without duplicates");settings.MoveMetric(2,-1);settings.MoveMetric(1,-1);Check(settings.Draft.RowsFor(Provider.OpenAI).SequenceEqual(new[]{RingContent.AllTimeTokens,RingContent.TodayTokens,RingContent.MonthTokens}),"Codex metrics reorder independently of other platforms");model.Configure(settings.Draft);model.Select(Provider.Dsh);
  Check(model.FirstMetricValue==model.Current!.Today.Tokens.Total&&model.SecondMetricValue==model.Current.Month.Tokens.Total&&model.ThirdMetricValue==(double?)model.Current.Balance,"DSH keeps its own default rows when Codex rows change");
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
