using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokNotch.Core.Models;
using TokNotch.UI.Controls;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;
internal static class BalanceValidation
{
 public static async Task RunAsync(IslandWindow window,IslandViewModel model)
 {
  var original=model.Snapshot!;var preferences=model.Preferences;var selected=model.Selected;var checks=new List<string>();
  void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
  var vendors=original.Vendors.Select(v=>v with {Balance=23.45m,Cash=20m,Voucher=3.45m,Currency="CNY",BalanceSource=v.Provider==Provider.Dsh?"DeepSeek":null,BalanceStatus="测试余额 · MOCK"}).ToArray();
  model.Apply(new(original.GeneratedAt,original.LastSuccessAt,original.BucketTimeZone,false,RefreshState.Ready,vendors,original.Models));
  model.Configure(new(preferences.Providers,new[]{UsageMetric.AllTime,UsageMetric.Month,UsageMetric.Today}));
  foreach(var provider in new[]{Provider.Dsh,Provider.Mimo,Provider.Kimi,Provider.DeepSeek}){
   model.Select(provider);await Task.Delay(50);window.UpdateLayout();var number=(AnimatedNumber)window.FindName("TokensNumber");
   Check(model.ThirdMetricValue==23.45&&number.Text=="¥23.45"&&model.ThirdMetricLabel.Contains("余额"),provider+" final row displays available balance");
  }
  model.Select(Provider.Dsh);await Task.Delay(50);window.UpdateLayout();Check(model.FirstMetricLabel=="本月"&&model.SecondMetricLabel=="今日"&&model.FirstMetricValue==model.Current!.Month.Tokens.Total,"daily/monthly ordering survives old cumulative-first preferences");
  Check(model.ThirdMetricLabel=="DeepSeek 余额","DSH balance row names the DeepSeek association instead of a bare amount");
  var output=ApplicationPaths.ArtifactsDirectory;
  void Shot(string name){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)(window.ActualWidth*1.5),(int)(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,name+".png"));encoder.Save(stream);}
  var image=new RenderTargetBitmap((int)(window.ActualWidth*1.5),(int)(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(output,"balance-layout-fixture.png")))png.Save(file);
  void Amount(decimal? value,string currency){var updated=vendors.Select(v=>v.Provider==Provider.Dsh?v with {Balance=value,Currency=currency}:v);model.Apply(new(original.GeneratedAt,original.LastSuccessAt,original.BucketTimeZone,false,RefreshState.Ready,updated,original.Models));}
  var ring=(RingChart)window.FindName("UsageRing");
  Amount(null,"CNY");await Task.Delay(50);Check(((AnimatedNumber)window.FindName("TokensNumber")).Text=="—","unknown balance rendered as dash");
  Check(model.RingFraction is null&&ring.DisplayedFraction is null&&model.RingCenterPrimary=="—","unknown balance keeps a neutral ring and a dash in the center, never a zero ring");
  Amount(0m,"CNY");await Task.Delay(50);Check(((AnimatedNumber)window.FindName("TokensNumber")).Text=="¥0.00","known zero rendered as amount");
  Check(model.RingFraction==0d&&ring.DisplayedFraction==0d,"known zero draws an empty ring but is not treated as unknown");
  Amount(25m,"CNY");await Task.Delay(50);Check(model.RingFraction==0.5d&&model.RingCenterPrimary=="¥25.00","half of the ¥50 baseline draws half a circle while the center keeps the real amount");Shot("dsh-amount-ring-half");
  Amount(75m,"CNY");await Task.Delay(50);Check(model.RingFraction==1d&&model.RingCenterPrimary=="¥75.00","above the baseline stays a full circle without truncating the amount");
  Amount(-1.25m,"USD");await Task.Delay(50);Check(((AnimatedNumber)window.FindName("TokensNumber")).Text=="-$1.25","USD negative balance uses matching currency");
  Check(model.RingFraction is null&&model.RingCenterPrimary=="-$1.25","USD balance without a USD baseline shows the real amount but no proportion");
  model.Configure(new(preferences.Providers,preferences.Metrics,50m,7.5m));Amount(3.75m,"USD");await Task.Delay(50);
  Check(Math.Abs(model.RingFraction!.Value-0.5)<1e-9,"configured USD baseline drives the USD ring independently of ¥50");
  model.Configure(preferences);Amount(23.45m,"CNY");await Task.Delay(50);
  model.Configure(new(new[]{Provider.DeepSeek,Provider.Dsh,Provider.Mimo},model.Preferences.Metrics));model.Select(Provider.DeepSeek);var unknown=vendors.Select(v=>v.Provider==Provider.DeepSeek?v with {TokenUsageAvailable=false}:v);model.Apply(new(original.GeneratedAt,original.LastSuccessAt,original.BucketTimeZone,false,RefreshState.Ready,unknown,original.Models));await Task.Delay(50);
  Check(model.FirstMetricValue==null&&model.SecondMetricValue==null&&model.PaybackValue==null&&((AnimatedNumber)window.FindName("TokensNumber")).Text=="¥23.45","DeepSeek known balance with unknown token history never shows zero tokens");
  window.UpdateLayout();var deepImage=new RenderTargetBitmap((int)(window.ActualWidth*1.5),(int)(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);deepImage.Render(window);var deepPng=new PngBitmapEncoder();deepPng.Frames.Add(BitmapFrame.Create(deepImage));using(var file=File.Create(Path.Combine(output,"deepseek-balance-fixture.png")))deepPng.Save(file);
  var codexRing=new RingSnapshot("codex.5h",0.72,"五小时剩余额度"){WindowLabel="5h",RecordedAt=DateTimeOffset.Now.AddSeconds(-40),ResetsAt=DateTimeOffset.Now.AddHours(2)};
  var codexInner=new RingSnapshot("codex.week",0.85,"一周剩余额度"){WindowLabel="周",RecordedAt=DateTimeOffset.Now.AddSeconds(-40),ResetsAt=DateTimeOffset.Now.AddDays(3)};
  var codex=vendors.Select(v=>v.Provider==Provider.OpenAI?v with {Ring=codexRing,InnerRing=codexInner}:v);
  model.Configure(new(new[]{Provider.OpenAI,Provider.Dsh,Provider.DeepSeek},preferences.Metrics));model.Select(Provider.OpenAI);
  model.Apply(new(original.GeneratedAt,original.LastSuccessAt,original.BucketTimeZone,false,RefreshState.Ready,codex,original.Models));await Task.Delay(50);window.UpdateLayout();
  Check(model.ShowInnerRing&&Math.Abs(model.RingFraction!.Value-0.72)<1e-9&&Math.Abs(model.InnerRingFraction!.Value-0.85)<1e-9,"Codex draws two independent quota rings");
  Check(model.RingCenterPrimary=="5h 72%"&&model.RingCenterSecondary=="周 85%","Codex ring center names both windows instead of colour only");
  Check(model.CenterLabel=="额度快照"&&model.RingTooltip.Contains("重置")&&model.RingTooltip.Contains("记录"),"fresh Codex snapshot labels the record and reset times on hover");
  Check(ring.ShowInner&&ring.DisplayedInnerFraction is not null,"inner ring renders only for the two-window source");
  var stale=codexRing with {RecordedAt=DateTimeOffset.Now.AddMinutes(-40)};model.Apply(new(original.GeneratedAt,original.LastSuccessAt,original.BucketTimeZone,false,RefreshState.Ready,codex.Select(v=>v.Provider==Provider.OpenAI?v with {Ring=stale}:v),original.Models));await Task.Delay(50);
  Check(model.CenterLabel=="上次记录","an old Codex snapshot is labelled as the last record rather than live data");
  var elapsed=codexRing with {ResetsAt=DateTimeOffset.Now.AddMinutes(-5),ResetElapsed=true};model.Apply(new(original.GeneratedAt,original.LastSuccessAt,original.BucketTimeZone,false,RefreshState.Ready,codex.Select(v=>v.Provider==Provider.OpenAI?v with {Ring=elapsed}:v),original.Models));await Task.Delay(50);
  Check(model.CenterLabel=="上次记录"&&Math.Abs(model.RingFraction!.Value-0.72)<1e-9&&model.RingTooltip.Contains("已过重置时间"),"past reset time flags the stale window and never rewrites it to 100%");
  model.Select(Provider.OpenAI);await Task.Delay(50);window.UpdateLayout();var codexImage=new RenderTargetBitmap((int)(window.ActualWidth*1.5),(int)(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);codexImage.Render(window);var codexPng=new PngBitmapEncoder();codexPng.Frames.Add(BitmapFrame.Create(codexImage));using(var stream=File.Create(Path.Combine(output,"codex-quota-rings.png")))codexPng.Save(stream);
  var choices=preferences.Rings.ToDictionary(pair=>pair.Key,pair=>pair.Value);choices[Provider.Dsh]=new(RingContent.TodayTokens,RingContent.Balance,500);choices[Provider.Kimi]=new(RingContent.CashBalance,RingContent.GiftBalance);
  var styled=new DisplayPreferences(new[]{Provider.Dsh,Provider.OpenAI,Provider.Kimi},preferences.Metrics,50m,rings:choices);
  var tokenPeriod=new PeriodUsage(new(250,0),null,CostStatus.Unavailable);var monthPeriod=new PeriodUsage(new(1000,0),null,CostStatus.Unavailable);
  var configured=vendors.Select(v=>v.Provider==Provider.Dsh?v with {Today=tokenPeriod,Month=monthPeriod,Balance=25m,Detection=DetectionState.Ready,TokenUsageAvailable=true}:v);
  model.Configure(styled);model.Select(Provider.Dsh);model.Apply(new(original.GeneratedAt,original.LastSuccessAt,original.BucketTimeZone,false,RefreshState.Ready,configured,original.Models));await Task.Delay(50);
  Check(model.RingFraction==0.5d&&model.InnerRingFraction==0.5d&&model.RingCenterPrimary=="250"&&model.RingCenterSecondary=="¥25.00","custom DSH rings use independent Token and money scales with matching center values");
  Check(model.RingTooltip.Contains("满圈 500")&&model.RingTooltip.Contains("满圈 ¥50"),"custom ring tooltip names both scales");Shot("custom-dsh-rings");
  model.Select(Provider.Kimi);await Task.Delay(50);Check(model.RingFraction==0.4d&&model.InnerRingFraction==0.069d,"Kimi cash and gift can be selected independently");
  choices[Provider.Dsh]=new(RingContent.TodayOfMonth,RingContent.None);model.Configure(new(styled.Providers,styled.Metrics,50m,rings:choices));model.Select(Provider.Dsh);
  var emptyMonth=configured.Select(v=>v.Provider==Provider.Dsh?v with {Month=new PeriodUsage(new(0,0),null,CostStatus.Unavailable)}:v);model.Apply(new(original.GeneratedAt,original.LastSuccessAt,original.BucketTimeZone,false,RefreshState.Ready,emptyMonth,original.Models));await Task.Delay(50);
  Check(model.RingFraction is null&&model.RingCenterPrimary=="—"&&!model.ShowInnerRing,"today share with no monthly denominator stays unknown and closes the inner ring");
  model.Apply(original);model.Configure(preferences);model.Select(selected);await File.WriteAllLinesAsync(Path.Combine(output,"balance-ui-checks.txt"),checks);
  }
}

