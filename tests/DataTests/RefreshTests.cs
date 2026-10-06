using System.Net;
using System.Text.Json;
using TokNotch.Core.Interaction;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Settings;
using TokNotch.Infrastructure.Usage;
internal static class RefreshTests
{
 public static async Task Run(string root)
 {
  int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}

  var scheduler=new RefreshScheduler(_=>Task.CompletedTask){Interval=TimeSpan.FromMinutes(30)};
  using(var stop=new CancellationTokenSource()){
   var loop=scheduler.Start(stop.Token);
   await Task.Delay(250);Check(scheduler.Completions==0,"a long interval does not fire early");
   scheduler.Interval=TimeSpan.FromMilliseconds(60);scheduler.Reschedule();
   await Task.Delay(400);Check(scheduler.Completions>=3,"the loop keeps firing at the configured interval");
   Check(scheduler.LastCompletionAt is not null,"the scheduler records when it last completed");
   var before=scheduler.Completions;scheduler.Reschedule();await Task.Delay(200);
   Check(scheduler.Completions>before,"rescheduling applies the new interval immediately instead of waiting out the old one");
   stop.Cancel();try{await loop;}catch(OperationCanceledException){}
   var after=scheduler.Completions;await Task.Delay(200);Check(scheduler.Completions==after,"cancelling stops the loop for good");
  }
  var failing=new RefreshScheduler(_=>throw new InvalidOperationException("fixture")){Interval=TimeSpan.FromMilliseconds(50)};
  using(var stopFailing=new CancellationTokenSource()){var loop=failing.Start(stopFailing.Token);await Task.Delay(300);stopFailing.Cancel();try{await loop;}catch(OperationCanceledException){}}
  Check(failing.Failures>=3&&failing.Completions==0,"a throwing refresh is counted as a failure and never kills the loop");

  var handler=new BalanceHandler();
  using(var deep=new DeepSeekApiSource(new HttpClient(handler))){
   deep.Configure("fixture-key");var first=await deep.ReadAsync(Local(Provider.DeepSeek),default);
   Check(handler.Requests==1&&first.Balance==110m,"the first balance query runs immediately");
   deep.Interval=TimeSpan.FromHours(1);var cached=await deep.ReadAsync(Local(Provider.DeepSeek),default);
   Check(handler.Requests==1&&cached.Balance==110m,"a long balance interval is honoured instead of re-querying every island refresh");
   deep.RequestRefresh();await deep.ReadAsync(Local(Provider.DeepSeek),default);
   Check(handler.Requests==2,"the manual refresh ignores the interval and queries at once");
  }
  var kimiHandler=new KimiHandler();
  using(var kimi=new KimiBalanceSource(new HttpClient(kimiHandler))){
   kimi.Configure("fixture-key");await kimi.ReadAsync(default);kimi.Interval=TimeSpan.FromHours(1);await kimi.ReadAsync(default);
   Check(kimiHandler.Requests==1,"Kimi honours the configured balance interval");kimi.RequestRefresh();await kimi.ReadAsync(default);
   Check(kimiHandler.Requests==2,"Kimi manual refresh still bypasses the interval");
  }

  var preferences=new DisplayPreferences(DisplayPreferences.Default.Providers,DisplayPreferences.Default.Metrics,50m,null,60,900);
  var folder=Path.Combine(root,"refresh-settings");var store=new DisplayPreferencesStore(folder);store.Save(preferences);
  var loaded=new DisplayPreferencesStore(folder).Load();
  Check(loaded.RefreshSeconds==60&&loaded.BalanceRefreshSeconds==900,"both refresh intervals survive a restart");
  Check(DisplayPreferences.Default.RefreshSeconds==30&&DisplayPreferences.Default.BalanceRefreshSeconds==300,"defaults stay 30s for the island and 300s for balances");
  bool rejected=false;try{_=new DisplayPreferences(preferences.Providers,preferences.Metrics,50m,null,4,300);}catch(ArgumentException){rejected=true;}Check(rejected,"an interface interval below 5s is rejected");
  rejected=false;try{_=new DisplayPreferences(preferences.Providers,preferences.Metrics,50m,null,30,10);}catch(ArgumentException){rejected=true;}Check(rejected,"a balance interval below 30s is rejected");
  var legacy=Path.Combine(root,"refresh-legacy");Directory.CreateDirectory(Path.Combine(legacy,"config"));
  File.WriteAllText(Path.Combine(legacy,"config","display.json"),"{\"Providers\":[\"OpenAI\",\"Dsh\",\"Kimi\"],\"Metrics\":[\"Today\",\"Month\",\"AllTime\"]}");
  var legacyLoaded=new DisplayPreferencesStore(legacy).Load();Check(legacyLoaded.RefreshSeconds==30&&legacyLoaded.BalanceRefreshSeconds==300,"an old settings file without the new fields keeps the defaults");
  File.WriteAllText(Path.Combine(legacy,"config","display.json"),"{\"Providers\":[\"OpenAI\",\"Dsh\",\"Kimi\"],\"Metrics\":[\"Today\",\"Month\",\"AllTime\"],\"RefreshSeconds\":1,\"BalanceRefreshSeconds\":999999}");
  var clamped=new DisplayPreferencesStore(legacy).Load();Check(clamped.RefreshSeconds==5&&clamped.BalanceRefreshSeconds==86400,"a hand-edited out-of-range interval is clamped instead of discarding the rest of the settings");
  Check(clamped.Expansion==ExpansionMode.Hover&&clamped.GlassEnabled,"an old settings file also defaults to hover expansion and liquid glass");
  var window=Path.Combine(root,"window-settings");var windowStore=new DisplayPreferencesStore(window);
  windowStore.Save(new DisplayPreferences(preferences.Providers,preferences.Metrics,50m,null,30,300,ExpansionMode.AlwaysExpanded,false));
  var windowLoaded=new DisplayPreferencesStore(window).Load();
  Check(windowLoaded.Expansion==ExpansionMode.AlwaysExpanded&&!windowLoaded.GlassEnabled,"expansion mode and material survive a restart");
  Check(DisplayPreferences.Default.Expansion==ExpansionMode.Hover&&DisplayPreferences.Default.GlassEnabled,"defaults stay hover expansion and liquid glass");
  rejected=false;try{_=new DisplayPreferences(preferences.Providers,preferences.Metrics,50m,null,30,300,(ExpansionMode)99,true);}catch(ArgumentException){rejected=true;}Check(rejected,"an undefined expansion mode is rejected");
  File.WriteAllText(Path.Combine(window,"config","display.json"),"{\"Providers\":[\"OpenAI\",\"Dsh\",\"Kimi\"],\"Metrics\":[\"Today\",\"Month\",\"AllTime\"],\"Expansion\":\"Telepathy\"}");
  var badEnum=new DisplayPreferencesStore(window);Check(badEnum.Load().Expansion==ExpansionMode.Hover&&badEnum.LoadWarning is not null,"an unknown expansion mode falls back to the defaults with a warning");
  Console.WriteLine($"{checks} refresh checks passed");
 }
 private static ProviderUsageSnapshot Local(Provider provider){var zero=new PeriodUsage(new(0,0),null,CostStatus.Unavailable);return new(provider,"x","X","X",DetectionState.Ready,zero,zero,zero,zero,null,new("x",null,"可用余额"));}
 private sealed class BalanceHandler:HttpMessageHandler
 {
  public int Requests;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();Requests++;if(request.RequestUri!.AbsoluteUri!="https://api.deepseek.com/user/balance")throw new Exception("unexpected endpoint");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"110\",\"granted_balance\":\"10\",\"topped_up_balance\":\"100\"}]}")});}
 }
 private sealed class KimiHandler:HttpMessageHandler
 {
  public int Requests;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();Requests++;if(request.RequestUri!.AbsoluteUri!="https://api.moonshot.cn/v1/users/me/balance")throw new Exception("unexpected endpoint");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"code\":0,\"status\":true,\"data\":{\"available_balance\":12.5,\"cash_balance\":-1,\"voucher_balance\":13.5}}")});}
 }
}
