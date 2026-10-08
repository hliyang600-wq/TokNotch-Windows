using System.Net;
using System.Text;
using System.Text.Json;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Authentication;
using TokNotch.Infrastructure.Settings;
using TokNotch.Infrastructure.Usage;
internal static class QwenPlanTests
{
 public static async Task Run(string root)
 {
  int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
  var handler=new Handler();var clock=new Clock();using var source=new QwenPlanSource(new HttpClient(handler),clock);
  var empty=await source.ReadAsync(default);Check(empty.Ring.Fraction is null&&!empty.TokenUsageAvailable&&!empty.RefreshFailed&&handler.Count==0,"unconnected Qwen quota is unknown and consumes no network or fabricated tokens");
  source.Configure("session=fixture-cookie");var result=await source.ReadAsync(default);
  Check(result.Provider==Provider.Qwen&&result.Detection==DetectionState.Ready&&result.Ring.Fraction==.75&&result.Balance is null,"Qwen monthly used fraction .25 becomes .75 remaining, not yuan or raw tokens");
  Check(result.Ring.ResetsAt==handler.Reset&&result.Ring.RecordedAt!=null,"Qwen millisecond reset timestamp is retained");
  int before=handler.Count;await source.ReadAsync(default);Check(handler.Count==before,"Qwen automatic refresh reuses quota within configured interval");
  Check(result.TokenUsageAvailable&&result.Today.Tokens.Total==120&&result.Today.Tokens.Input==60&&result.Today.Tokens.CacheRead==40&&result.Today.Tokens.Output==20,"daily tokens exclude cumsum, tools, image subsets and yesterday; cached input is counted once");
  Check(handler.Start==new DateTimeOffset(clock.Now.Year,clock.Now.Month,clock.Now.Day,0,0,0,TimeSpan.FromHours(8)).ToUnixTimeMilliseconds(),"Qwen today request uses UTC+8 midnight and the personal plan, independent of system timezone");
  handler.Used=.8;source.RequestRefresh();result=await source.ReadAsync(default);Check(handler.Count==before+3&&Math.Abs(result.Ring.Fraction!.Value-.2)<.0001,"forced Qwen refresh rereads user token, actual quota and daily tokens");
  handler.Problem="missing";handler.Used=.4;source.RequestRefresh();var partial=await source.ReadAsync(default);Check(partial.RefreshFailed&&partial.TokenUsageAvailable&&partial.Today.Tokens.Total==120&&partial.Ring.Fraction==.6&&partial.SourceStatus!.Contains("月余量已更新"),"daily failure preserves same-day tokens while publishing freshly read monthly quota");
  source.Interval=TimeSpan.FromDays(1);clock.Now=clock.Now.AddDays(1);before=handler.Count;partial=await source.ReadAsync(default);Check(handler.Count==before+3&&partial.RefreshFailed&&!partial.TokenUsageAvailable,"UTC+8 midnight bypasses long cache and never labels yesterday's tokens as today's on failure");
  handler.Problem="empty";source.RequestRefresh();partial=await source.ReadAsync(default);Check(!partial.RefreshFailed&&partial.TokenUsageAvailable&&partial.Today.Tokens.Total==0,"successful empty personal daily series is valid zero, distinct from missing response");
  foreach(var problem in new[]{"fractional","negative","overflow","mismatch","cache"}){handler.Problem=problem;source.Configure("session=fixture-cookie");Check((await source.ReadAsync(default)).RefreshFailed,"invalid daily token data is not fabricated: "+problem);}
  handler.Problem="";handler.Download="https://fixture.oss-cn-beijing.aliyuncs.com/day.json?fixture=1";source.RequestRefresh();partial=await source.ReadAsync(default);Check(!partial.RefreshFailed&&partial.Today.Tokens.Total==120&&handler.AnonymousDownload,"official OSS daily data is read without console Cookie, authorization or secToken");
  foreach(var url in new[]{"https://attacker.example/day.json","http://fixture.oss-cn-beijing.aliyuncs.com/day.json","https://fixture.oss-cn-beijing.aliyuncs.com.attacker.example/day.json"}){handler.Download=url;source.Configure("session=fixture-cookie");before=handler.Count;partial=await source.ReadAsync(default);Check(partial.RefreshFailed&&!partial.TokenUsageAvailable&&handler.Count==before+3,"unsafe telemetry destination rejected without fetching: "+url);}
  handler.Download=null;source.Configure("session=fixture-cookie");result=await source.ReadAsync(default);
  handler.Status=HttpStatusCode.Unauthorized;source.RequestRefresh();var stale=await source.ReadAsync(default);Check(stale.RefreshFailed&&stale.Ring.Fraction==result.Ring.Fraction&&source.RequiresLogin&&stale.SourceStatus!.Contains("已过期"),"Qwen auth expiration retains prior quota and asks for login");
  handler.Status=HttpStatusCode.OK;handler.Used=0;source.RequestRefresh();Check((await source.ReadAsync(default)).Ring.Fraction==1&&!source.RequiresLogin,"successful Qwen retry clears expired state and valid zero usage means full remaining");
  handler.Used=1;source.RequestRefresh();Check((await source.ReadAsync(default)).Ring.Fraction==0,"Qwen exhausted monthly quota is a valid zero, distinct from unknown");
  handler.Used=25;source.Configure("session=fixture-cookie");Check((await source.ReadAsync(default)).Ring.Fraction is null,"old 0-to-100 percentage convention is rejected instead of silently producing a wrong quota");
  handler.Used=.2;handler.MonthMissing=true;source.Configure("session=fixture-cookie");Check((await source.ReadAsync(default)).RefreshFailed,"obsolete weekly fields are not relabelled as current monthly credits");handler.MonthMissing=false;
  handler.BusinessFailure=true;source.Configure("session=fixture-cookie");Check((await source.ReadAsync(default)).RefreshFailed,"HTTP 200 with nested business failure never becomes a successful quota");handler.BusinessFailure=false;
  handler.Reset=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.Now.AddMinutes(-1).ToUnixTimeMilliseconds());source.Configure("session=fixture-cookie");Check((await source.ReadAsync(default)).RefreshFailed,"elapsed quota reset cannot be marked fresh");handler.Reset=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.Now.AddDays(20).ToUnixTimeMilliseconds());
  bool rejected=false;try{source.Configure("session=x\r\nInjected: header");}catch(ArgumentException){rejected=true;}Check(rejected,"Qwen cookie header injection rejected");rejected=false;try{source.Configure("sk-sp-fixture");}catch(ArgumentException){rejected=true;}Check(rejected,"model key is not mistaken for a console session");
  var prefs=new DisplayPreferences(new[]{Provider.Qwen,Provider.OpenAI,Provider.Mimo},DisplayPreferences.Default.Metrics);var store=new DisplayPreferencesStore(Path.Combine(root,"qwen-display"));store.Save(prefs);var restored=store.Load();
  Check(restored.Providers[0]==Provider.Qwen&&restored.RingFor(Provider.Qwen).Outer==RingContent.MonthlyRemaining&&restored.RowsFor(Provider.Qwen).SequenceEqual(new[]{RingContent.TodayTokens,RingContent.QuotaResetTime,RingContent.None}),"Qwen today's tokens and reset rows survive settings restart with monthly remaining ring");
  rejected=false;try{RingChoices.Validate(Provider.Qwen,new(RingContent.Balance,RingContent.None));}catch(ArgumentException){rejected=true;}Check(rejected,"unsupported Qwen money ring is rejected");
  var vault=new ApiKeyVault(Path.Combine(root,"qwen-vault"));vault.SetDeepSeek("deep-fixture");vault.SetKimi("kimi-fixture");vault.SetQwenCookie("session=fixture-cookie");
  Check(vault.Load()?.QwenCookie=="session=fixture-cookie"&&!Encoding.UTF8.GetString(File.ReadAllBytes(vault.FilePath)).Contains("fixture-cookie"),"Qwen cookie is restored via DPAPI and never persisted in plaintext");
  vault.SetDeepSeek("updated");vault.SetKimi(null);Check(vault.Load()?.QwenCookie=="session=fixture-cookie","editing and clearing other keys preserves Qwen session");vault.SetQwenCookie(null);Check(vault.Load()?.DeepSeek=="updated"&&vault.Load()?.QwenCookie is null,"disconnecting Qwen preserves other provider key");
  Console.WriteLine($"{checks} Qwen plan checks passed");
 }
 private sealed class Handler:HttpMessageHandler
 {
  public int Count;public double Used=.25;public bool MonthMissing,BusinessFailure,AnonymousDownload;public HttpStatusCode Status=HttpStatusCode.OK;public string Problem="";public string? Download;public long Start,End;
  public DateTimeOffset Reset=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.Now.AddDays(20).ToUnixTimeMilliseconds());
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   Count++;var uri=request.RequestUri!;
   if(uri.Host=="fixture.oss-cn-beijing.aliyuncs.com"){AnonymousDownload=!request.Headers.Contains("Cookie")&&!request.Headers.Contains("Authorization");return new(Status){Content=new StringContent(JsonSerializer.Serialize(Series()))};}
   if(!request.Headers.GetValues("Cookie").Single().Contains("fixture-cookie"))throw new Exception("Missing fixture cookie");
   if(uri.Host=="platform-home.qianwenai.com"&&request.Method==HttpMethod.Get&&uri.AbsolutePath=="/tool/user/info.json")return new(Status){Content=new StringContent("{\"data\":{\"secToken\":\"fixture-sec\"}}")};
   if(uri.Host!="cs-data.qianwenai.com"||request.Method!=HttpMethod.Post||uri.AbsolutePath!="/data/api.json")throw new Exception("Unexpected credential destination");
   var form=(await request.Content!.ReadAsStringAsync(ct)).Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>Uri.UnescapeDataString(p[1].Replace('+',' ')));
   using var param=JsonDocument.Parse(form["params"]);
   if(form["sec_token"]!="fixture-sec"||form["region"]!="cn-beijing"||param.RootElement.GetProperty("Data").GetProperty("cornerstoneParam").GetProperty("domain").GetString()!="platform.qianwenai.com")throw new Exception("Incorrect official query");
   if(param.RootElement.GetProperty("Api").GetString()=="zeldaEasy.bailian-telemetry.platform-model.getModelMonitorDataWithOss"){
    var req=param.RootElement.GetProperty("Data").GetProperty("reqDTO");Start=req.GetProperty("startTime").GetInt64();End=req.GetProperty("endTime").GetInt64();
    if(req.GetProperty("productMode").GetString()!="TokenPlanPersonal"||req.GetProperty("step").GetInt32()!=86400||End-Start!=86400000||req.GetProperty("metricFilters")[0].GetProperty("metricName").GetString()!="model_usage")throw new Exception("Incorrect personal daily query");
    object body=Download is not null?new{dataDownloadUrl=Download}:Problem=="missing"?new{}:new{originData=Series()};
    return new(Status){Content=new StringContent(JsonSerializer.Serialize(new{successResponse=true,data=new{success=true,DataV2=new{data=new{success=true,data=body}}}}))};
   }
   if(param.RootElement.GetProperty("Api").GetString()!="zeldaHttp.apikeyMgr./tokenplan/personal/api/v2/usage")throw new Exception("Unexpected business API");
   object usage=MonthMissing?new{per1WeekPercentage=Used}:new{per1MonthPercentage=Used,per1MonthResetTime=Reset.ToUnixTimeMilliseconds()};
   return new(Status){Content=new StringContent(JsonSerializer.Serialize(new{successResponse=true,data=new{success=!BusinessFailure,DataV2=new{data=new{success=true,data=usage}}}}))};
  }
  private object[] Series(){
   if(Problem=="empty")return Array.Empty<object>();
   object Item(string type,object value,string agg="sum",string unit="tokens")=>new{aggMethod=agg,metricName="model_usage",step=86400,labels=new{usage_type=type,unit},points=new[]{new{timestamp=Start,value=(object)9999},new{timestamp=End,value}}};
   object total=Problem switch{"fractional"=>"120.5","negative"=>-1,"overflow"=>"9223372036854775808","mismatch"=>121,_=>120};
   return new[]{Item("total_tokens",total),Item("input_tokens","100"),Item("output_tokens",20),Item("cached_tokens",Problem=="cache"?101:40),Item("total_tokens",120,"cumsum"),Item("image_tokens",1000),Item("tool",999,"sum","times")};
  }
 }
 private sealed class Clock:TimeProvider{public DateTimeOffset Now=new(2026,10,9,23,59,0,TimeSpan.FromHours(8));public override DateTimeOffset GetUtcNow()=>Now.ToUniversalTime();}
}
