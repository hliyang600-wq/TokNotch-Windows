using System.Net;
using System.Text.Json;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Settings;
using TokNotch.Infrastructure.Usage;
internal static class SettingsAndMimoTests
{
 public static async Task Run(string root)
 {
  int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
  var path=Path.Combine(root,"settings-test");var store=new DisplayPreferencesStore(path);var configured=new DisplayPreferences(new[]{Provider.Mimo,Provider.OpenAI,Provider.Dsh},new[]{UsageMetric.AllTime,UsageMetric.Today,UsageMetric.Month},120m,7.5m);store.Save(configured);var loaded=new DisplayPreferencesStore(path).Load();Check(loaded.Providers.SequenceEqual(configured.Providers)&&loaded.Metrics.SequenceEqual(configured.Metrics)&&loaded.AmountBaselineCny==120m&&loaded.AmountBaselineUsd==7.5m,"provider, metric order and both amount baselines survive restart");
  store.Save(new DisplayPreferences(configured.Providers,configured.Metrics));var defaults=new DisplayPreferencesStore(path).Load();Check(defaults.AmountBaselineCny==50m&&defaults.AmountBaselineUsd is null,"missing baseline fields fall back to ¥50 and an unset USD baseline");
  bool rejected=false;try{_ = new DisplayPreferences(new[]{Provider.Kimi,Provider.Kimi,Provider.Dsh},configured.Metrics);}catch(ArgumentException){rejected=true;}Check(rejected,"duplicate visible providers rejected");
  var rowOrder=configured.Rows.ToDictionary(p=>p.Key,p=>p.Value);
  var edited=new[]{RingContent.Balance,RingContent.AllTimeTokens,RingContent.MonthTokens};rowOrder[Provider.Dsh]=edited;
  rowOrder[Provider.OpenAI]=new[]{RingContent.WeeklyRemaining,RingContent.TodayTokens,RingContent.FiveHourRemaining};
  rowOrder[Provider.Kimi]=new[]{RingContent.Balance,RingContent.GiftBalance,RingContent.CashBalance};
  var perProvider=new DisplayPreferences(configured.Providers,configured.Metrics,120m,7.5m,rows:rowOrder);edited[0]=RingContent.None;
  Check(perProvider.RowsFor(Provider.Dsh)[0]==RingContent.Balance,"custom rows freeze caller arrays instead of sharing mutable settings");
  store.Save(perProvider);var perProviderLoaded=new DisplayPreferencesStore(path).Load();
  Check(ProviderCatalog.Available.All(p=>perProviderLoaded.RowsFor(p.Id).SequenceEqual(perProvider.RowsFor(p.Id))),"independent rows for all available platforms survive restart, including hidden platforms");
  Check(perProviderLoaded.WithWindow(new()).RowsFor(Provider.Dsh).SequenceEqual(perProvider.RowsFor(Provider.Dsh)),"appearance and position updates preserve custom display rows");
  Check(perProviderLoaded.RingFor(Provider.Dsh)==configured.RingFor(Provider.Dsh),"row selection leaves ring selection independent");
  var settingsFile=Path.Combine(path,"config","display.json");var document=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settingsFile))!.AsObject();document.Remove("Rows");File.WriteAllText(settingsFile,document.ToJsonString());var migrated=new DisplayPreferencesStore(path).Load();
  Check(migrated.RowsFor(Provider.OpenAI).SequenceEqual(new[]{RingContent.AllTimeTokens,RingContent.TodayTokens,RingContent.MonthTokens})&&migrated.RowsFor(Provider.Dsh).SequenceEqual(new[]{RingContent.TodayTokens,RingContent.MonthTokens,RingContent.Balance}),"legacy metric order migrates to the old visible layout for each provider");
  Check(migrated.RowsFor(Provider.Kimi).SequenceEqual(new[]{RingContent.CashBalance,RingContent.GiftBalance,RingContent.Balance}),"legacy Kimi money fields retain their meaning");
  rowOrder[Provider.Kimi]=new[]{RingContent.TodayTokens,RingContent.GiftBalance,RingContent.Balance};rejected=false;try{_=new DisplayPreferences(configured.Providers,configured.Metrics,rows:rowOrder);}catch(ArgumentException){rejected=true;}Check(rejected,"unsupported Kimi token fields are rejected rather than shown as zero");
  rowOrder[Provider.Kimi]=perProvider.RowsFor(Provider.Kimi);rowOrder[Provider.Dsh]=new[]{RingContent.Balance,RingContent.Balance,RingContent.MonthTokens};rejected=false;try{_=new DisplayPreferences(configured.Providers,configured.Metrics,rows:rowOrder);}catch(ArgumentException){rejected=true;}Check(rejected,"duplicate display rows are rejected at the configuration boundary");
  document["Rows"]=System.Text.Json.Nodes.JsonNode.Parse("{\"Dsh\":null}");File.WriteAllText(settingsFile,document.ToJsonString());var badRows=new DisplayPreferencesStore(path);_=badRows.Load();Check(badRows.LoadWarning!=null,"malformed null row list falls back safely with a warning");
  rowOrder[Provider.Dsh]=perProvider.RowsFor(Provider.Dsh);rowOrder[Provider.Kimi]=new[]{RingContent.Balance,RingContent.None,RingContent.None};store.Save(new(configured.Providers,configured.Metrics,rows:rowOrder));Check(store.Load().RowsFor(Provider.Kimi).Count(m=>m==RingContent.None)==2,"multiple hidden rows survive a restart");
  store.Save(DisplayPreferences.Default);Check(Directory.GetFiles(Path.Combine(path,"backups","settings")).Length>0,"previous settings backed up");File.WriteAllText(Path.Combine(path,"config","display.json"),"{broken");Check(new DisplayPreferencesStore(path).Load().Providers.SequenceEqual(DisplayPreferences.Default.Providers),"corrupt preferences fall back safely");
  var clock=new MimoClock();var handler=new MimoHandler(clock);using var client=new HttpClient(handler);using var source=new MimoUsageSource(client,clock){Interval=TimeSpan.FromDays(1)};Check((await source.ReadAsync(default)).Detection==DetectionState.Unavailable,"unconnected MiMo remains unknown");
  source.Configure("api-platform_serviceToken=fixture-session; api-platform_ph=fixture-ph; userId=123");var live=await source.ReadAsync(default);Check(live.Detection==DetectionState.Ready&&live.AllTime.Tokens.Total==650,"MiMo historical tokens include realtime and batch");Check(live.Month.Tokens.Total==150&&live.Month.Tokens.CacheRead==20&&live.Today.Tokens.Total==100,"MiMo UTC date buckets and cache misses counted correctly");Check(handler.CheckedPost,"MiMo read query uses verified UTC month payload and CSRF parameter");
  int before=handler.Requests;clock.Now=new(2026,10,31,16,1,0,TimeSpan.Zero);var midnight=await source.ReadAsync(default);
  Check(handler.Requests==before&&midnight.Today.Tokens.Total==100,"Beijing midnight keeps the current UTC day and cached usage");
  source.RequestRefresh();midnight=await source.ReadAsync(default);
  Check(handler.Requests==before+3&&handler.Month==10&&midnight.Today.Tokens.Total==100,"manual refresh after Beijing month rollover still queries October UTC");
  before=handler.Requests;clock.Now=new(2026,11,1,0,1,0,TimeSpan.Zero);var rolled=await source.ReadAsync(default);
  Check(handler.Requests==before+3&&handler.Month==11&&rolled.Today.Tokens.Total==150,"UTC day and month rollover bypasses a still-valid 24-hour cache");
  Check(rolled.SourceStatus!.Contains("2026-11-01 UTC")&&rolled.SourceStatus.Contains("5 分钟"),"MiMo status explains the bucket date and platform delay");
  int count=handler.Requests;await source.ReadAsync(default);Check(count==handler.Requests,"MiMo polling gate skips repeated network requests");handler.Status=HttpStatusCode.Unauthorized;source.RequestRefresh();var stale=await source.ReadAsync(default);Check(stale.Month.Tokens.Total==150&&stale.SourceStatus!.Contains("已过期"),"expired MiMo login preserves stale values");
  source.Configure("api-platform_serviceToken=fixture-session");Check((await source.ReadAsync(default)).Detection==DetectionState.Unavailable,"MiMo auth failure without prior value remains unknown");
  rejected=false;try{source.Configure("Cookie: fake\r\nInjected: header");}catch(ArgumentException){rejected=true;}Check(rejected,"newline cookie injection rejected");Console.WriteLine($"{checks} settings and MiMo checks passed");
 }
 private sealed class MimoClock:TimeProvider
 {
  public DateTimeOffset Now=new(2026,10,31,15,59,0,TimeSpan.Zero);
  public override DateTimeOffset GetUtcNow()=>Now;
  public override TimeZoneInfo LocalTimeZone=>TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
 }
 private sealed class MimoHandler(MimoClock clock):HttpMessageHandler
 {
  public int Requests,Month;public bool CheckedPost;public HttpStatusCode Status=HttpStatusCode.OK;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   ct.ThrowIfCancellationRequested();Requests++;if(request.RequestUri!.Host!="platform.xiaomimimo.com"||!request.Headers.GetValues("Cookie").Single().Contains("fixture-session"))throw new Exception("Unexpected credential destination");
   string payload;
   if(request.RequestUri.AbsolutePath=="/api/v1/usage"){if(request.Method!=HttpMethod.Get)throw new Exception("Expected GET summary");payload="{\"tokenUsage\":{\"inputToken\":400,\"outputToken\":100,\"cacheToken\":200,\"batchInputToken\":100,\"batchOutputToken\":50,\"batchCacheToken\":40,\"totalToken\":650}}";}
   else if(request.RequestUri.AbsolutePath=="/api/v1/balance"){payload="{\"balance\":\"23.45\",\"cashBalance\":\"20.00\",\"giftBalance\":\"3.45\",\"currency\":\"CNY\"}";}
   else if(request.RequestUri.AbsolutePath=="/api/v1/usage/detail/list"){
    var utc=clock.GetUtcNow();using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));if(request.Method!=HttpMethod.Post||body.RootElement.GetProperty("year").GetInt32()!=utc.Year||body.RootElement.GetProperty("month").GetInt32()!=utc.Month||!request.RequestUri.Query.Contains("api-platform_ph=fixture-ph"))throw new Exception("Unexpected UTC month request");CheckedPost=true;Month=utc.Month;
    payload=JsonSerializer.Serialize(new[]{new{date=utc.ToString("yyyy-MM-dd"),inputMissToken=70,outputToken=20,inputHitToken=10,totalToken=100},new{date=new DateTime(utc.Year,utc.Month,1).ToString("yyyy-MM-dd"),inputMissToken=30,outputToken=10,inputHitToken=10,totalToken=50}});
   }else throw new Exception("Unexpected console endpoint");
   return new HttpResponseMessage(Status){Content=new StringContent("{\"code\":0,\"data\":"+payload+"}")};
  }
 }
}
