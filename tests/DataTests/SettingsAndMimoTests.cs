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
  store.Save(DisplayPreferences.Default);Check(Directory.GetFiles(Path.Combine(path,"backups","settings")).Length>0,"previous settings backed up");File.WriteAllText(Path.Combine(path,"config","display.json"),"{broken");Check(new DisplayPreferencesStore(path).Load().Providers.SequenceEqual(DisplayPreferences.Default.Providers),"corrupt preferences fall back safely");
  var handler=new MimoHandler();using var client=new HttpClient(handler);using var source=new MimoUsageSource(client);Check((await source.ReadAsync(default)).Detection==DetectionState.Unavailable,"unconnected MiMo remains unknown");
  source.Configure("api-platform_serviceToken=fixture-session; api-platform_ph=fixture-ph; userId=123");var live=await source.ReadAsync(default);Check(live.Detection==DetectionState.Ready&&live.AllTime.Tokens.Total==650,"MiMo historical tokens include realtime and batch");Check(live.Month.Tokens.Total==150&&live.Month.Tokens.CacheRead==20&&live.Today.Tokens.Total==(DateTime.Now.Day==1?150:100),"MiMo date buckets and cache misses counted correctly");Check(handler.CheckedPost,"MiMo read query uses verified month payload and CSRF parameter");
  int count=handler.Requests;await source.ReadAsync(default);Check(count==handler.Requests,"MiMo polling gate skips repeated network requests");handler.Status=HttpStatusCode.Unauthorized;source.RequestRefresh();var stale=await source.ReadAsync(default);Check(stale.Month.Tokens.Total==150&&stale.SourceStatus!.Contains("已过期"),"expired MiMo login preserves stale values");
  source.Configure("api-platform_serviceToken=fixture-session");Check((await source.ReadAsync(default)).Detection==DetectionState.Unavailable,"MiMo auth failure without prior value remains unknown");
  rejected=false;try{source.Configure("Cookie: fake\r\nInjected: header");}catch(ArgumentException){rejected=true;}Check(rejected,"newline cookie injection rejected");Console.WriteLine($"{checks} settings and MiMo checks passed");
 }
 private sealed class MimoHandler:HttpMessageHandler
 {
  public int Requests;public bool CheckedPost;public HttpStatusCode Status=HttpStatusCode.OK;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   ct.ThrowIfCancellationRequested();Requests++;if(request.RequestUri!.Host!="platform.xiaomimimo.com"||!request.Headers.GetValues("Cookie").Single().Contains("fixture-session"))throw new Exception("Unexpected credential destination");
   string payload;
   if(request.RequestUri.AbsolutePath=="/api/v1/usage"){if(request.Method!=HttpMethod.Get)throw new Exception("Expected GET summary");payload="{\"tokenUsage\":{\"inputToken\":400,\"outputToken\":100,\"cacheToken\":200,\"batchInputToken\":100,\"batchOutputToken\":50,\"batchCacheToken\":40,\"totalToken\":650}}";}
   else if(request.RequestUri.AbsolutePath=="/api/v1/balance"){payload="{\"balance\":\"23.45\",\"cashBalance\":\"20.00\",\"giftBalance\":\"3.45\",\"currency\":\"CNY\"}";}
   else if(request.RequestUri.AbsolutePath=="/api/v1/usage/detail/list"){
    using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));if(request.Method!=HttpMethod.Post||body.RootElement.GetProperty("year").GetInt32()!=DateTime.Now.Year||body.RootElement.GetProperty("month").GetInt32()!=DateTime.Now.Month||!request.RequestUri.Query.Contains("api-platform_ph=fixture-ph"))throw new Exception("Unexpected month request");CheckedPost=true;
    payload=JsonSerializer.Serialize(new[]{new{date=DateTime.Now.ToString("yyyy-MM-dd"),inputMissToken=70,outputToken=20,inputHitToken=10,totalToken=100},new{date=new DateTime(DateTime.Now.Year,DateTime.Now.Month,1).ToString("yyyy-MM-dd"),inputMissToken=30,outputToken=10,inputHitToken=10,totalToken=50}});
   }else throw new Exception("Unexpected console endpoint");
   return new HttpResponseMessage(Status){Content=new StringContent("{\"code\":0,\"data\":"+payload+"}")};
  }
 }
}
