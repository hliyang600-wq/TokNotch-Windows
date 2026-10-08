using System.Globalization;
using System.Text.Json;
using TokNotch.Core.Models;
namespace TokNotch.Infrastructure.Usage;

/// <summary>Read-only personal monthly Credits quota and today's Tokens, matching console-home 1.1.44.</summary>
public sealed class QwenPlanSource : IDisposable
{
 private readonly HttpClient client;private readonly TimeProvider clock;
 private string? cookie;private ProviderUsageSnapshot? previous;private DateTimeOffset next;private int revision;
 private DateOnly? queriedDay,tokensDay;
 public TimeSpan Interval {get;set;}=TimeSpan.FromMinutes(5);
 public bool RequiresLogin {get;private set;}
 public QwenPlanSource(HttpClient? httpClient=null,TimeProvider? timeProvider=null){client=httpClient??new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(15)};client.MaxResponseContentBufferSize=4*1024*1024;clock=timeProvider??TimeProvider.System;}
 public void Configure(string header){if(header.Length>8192||header.Contains('\r')||header.Contains('\n')||header.TrimStart().StartsWith("sk-",StringComparison.Ordinal))throw new ArgumentException("请输入千问控制台的一行 Cookie，不是模型 API key。");cookie=header.Trim();previous=null;next=DateTimeOffset.MinValue;queriedDay=tokensDay=null;revision++;RequiresLogin=false;}
 public void RequestRefresh()=>next=DateTimeOffset.MinValue;
 private static ProviderUsageSnapshot Empty(string status){var zero=new PeriodUsage(new(0,0),null,CostStatus.Unavailable);return new(Provider.Qwen,"qwen-plan","千问 Token Plan","千问 Token Plan",DetectionState.Unavailable,zero,zero,zero,zero,null,new("qwen-plan",null,"月剩余额度")){TokenUsageAvailable=false,SourceStatus=status};}
 public async Task<ProviderUsageSnapshot> ReadAsync(CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(cookie))return Empty("未连接 · 设置 → 数据连接 → 登录千问");
  var now=clock.GetUtcNow();var local=now.ToOffset(TimeSpan.FromHours(8));var day=DateOnly.FromDateTime(local.DateTime);
  var midnight=new DateTimeOffset(local.Year,local.Month,local.Day,0,0,0,local.Offset);
  if(now<next&&queriedDay==day&&previous!=null&&(previous.Ring.ResetsAt is not {} reset||now<reset))return previous;
  next=now+Interval;queriedDay=day;var version=revision;var activeCookie=cookie;
  try{
   using var info=await Query("https://platform-home.qianwenai.com/tool/user/info.json",null,activeCookie,ct);
   if(!info.RootElement.TryGetProperty("data",out var user)||!user.TryGetProperty("secToken",out var token)||token.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(token.GetString()))throw new LoginException();
   using var usage=await Business("zeldaHttp.apikeyMgr./tokenplan/personal/api/v2/usage",token.GetString()!,activeCookie,null,ct);
   var data=Body(usage);
   var used=data.GetProperty("per1MonthPercentage").GetDouble();if(!double.IsFinite(used)||used<0||used>1)throw new JsonException();
   DateTimeOffset? resets=data.TryGetProperty("per1MonthResetTime",out var time)&&time.ValueKind==JsonValueKind.Number&&time.TryGetInt64(out var milliseconds)&&milliseconds>0?DateTimeOffset.FromUnixTimeMilliseconds(milliseconds):null;
   var result=Empty($"Token Plan 个人版 · 今日 Token 按 UTC+8 统计 · {now.ToLocalTime():HH:mm:ss} 查询") with {Detection=DetectionState.Ready,Ring=new("qwen-plan",1-used,"月剩余额度"){RecordedAt=now,ResetsAt=resets,ResetElapsed=resets<=now}};
   if(result.Ring.ResetElapsed)throw new JsonException();
   try{
    using var daily=await Business("zeldaEasy.bailian-telemetry.platform-model.getModelMonitorDataWithOss",token.GetString()!,activeCookie,new{productMode="TokenPlanPersonal",startTime=midnight.ToUnixTimeMilliseconds(),endTime=midnight.AddDays(1).ToUnixTimeMilliseconds(),step=86400,metricFilters=new[]{new{aggMethod="sum",metricName="model_usage"}}},ct);
    var body=Body(daily);TokenCounts counts;
    if(body.TryGetProperty("dataDownloadUrl",out var download)&&download.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(download.GetString())){
     if(!Uri.TryCreate(download.GetString(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Port!=443||uri.UserInfo.Length!=0||!uri.Host.EndsWith(".aliyuncs.com",StringComparison.OrdinalIgnoreCase))throw new JsonException();
     // Official OSS downloads contain telemetry only; never forward the console Cookie or secToken.
     using var response=await client.GetAsync(uri,ct);response.EnsureSuccessStatusCode();using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));counts=Today(document.RootElement,midnight);
    }else counts=Today(body.GetProperty("originData"),midnight);
    result=result with{Today=new(counts,null,CostStatus.Unavailable),TokenUsageAvailable=true};
    if(version==revision)tokensDay=day;
   }catch(OperationCanceledException)when(!ct.IsCancellationRequested){DailyFailed("今日 Token 查询超时");}
   catch(Exception error)when(IsSourceError(error)){DailyFailed(error is LoginException?"今日 Token 登录已过期 · 请重新登录":"今日 Token 查询失败 · 稍后刷新");if(version==revision)RequiresLogin=error is LoginException;}
   if(version!=revision)return previous??Empty("连接已更改 · 待刷新");if(!result.RefreshFailed)RequiresLogin=false;previous=result;return result;
   void DailyFailed(string message){var sameDay=tokensDay==day&&previous?.TokenUsageAvailable==true;result=result with{RefreshFailed=true,Today=sameDay?previous!.Today:result.Today,TokenUsageAvailable=sameDay,SourceStatus=message+(sameDay?" · 今日显示上次数据":" · 今日未知")+" · 月余量已更新 · UTC+8"};}
  }catch(OperationCanceledException)when(!ct.IsCancellationRequested){return Fail("千问余量请求超时");}
  catch(Exception error)when(IsSourceError(error)){return Fail(error is LoginException?"千问登录已过期 · 请重新登录":"千问月余量查询失败 · 请检查会话或稍后刷新",error is LoginException);}
  ProviderUsageSnapshot Fail(string message,bool login=false){if(version!=revision)return previous??Empty("连接已更改 · 待刷新");RequiresLogin=login;previous=previous is null?Empty(message) with {RefreshFailed=true}:previous with {RefreshFailed=true,TokenUsageAvailable=tokensDay==day&&previous.TokenUsageAvailable,SourceStatus=message+" · 上次额度（已过期）"};return previous;}
 }
 private async Task<JsonDocument> Business(string api,string token,string header,object? req,CancellationToken ct)
 {
  var data=new Dictionary<string,object>{{"cornerstoneParam",new{domain="platform.qianwenai.com",consoleSite="QIANWENAI",console="ONE_CONSOLE",xsp_lang="zh-CN",protocol="V2",productCode="p_efm"}}};if(req!=null)data["reqDTO"]=req;
  return await Query("https://cs-data.qianwenai.com/data/api.json?product=sfm_bailian&action=BroadScopeAspnGateway&api="+Uri.EscapeDataString(api),new Dictionary<string,string>{{"product","sfm_bailian"},{"action","BroadScopeAspnGateway"},{"sec_token",token},{"region","cn-beijing"},{"params",JsonSerializer.Serialize(new{Api=api,Data=data,V="1.0"})}},header,ct);
 }
 private static JsonElement Body(JsonDocument document)
 {
  var root=document.RootElement;
  if(!(root.TryGetProperty("successResponse",out var success)&&success.ValueKind==JsonValueKind.True)&&!(root.TryGetProperty("code",out var code)&&code.ToString()=="200"))throw new JsonException();
  var data=root.GetProperty("data");CheckSuccess(data);data=data.GetProperty("DataV2").GetProperty("data");CheckSuccess(data);
  if(data.TryGetProperty("data",out var body)){data=body;CheckSuccess(data);}return data;
 }
 private static TokenCounts Today(JsonElement series,DateTimeOffset midnight)
 {
  if(series.ValueKind!=JsonValueKind.Array)throw new JsonException();
  var totals=new Dictionary<string,long>();var start=midnight.ToUnixTimeMilliseconds();var end=midnight.AddDays(1).ToUnixTimeMilliseconds();
  foreach(var item in series.EnumerateArray()){
   if(item.GetProperty("aggMethod").GetString()!="sum"||item.GetProperty("metricName").GetString()!="model_usage")continue;
   var labels=item.GetProperty("labels");var type=labels.GetProperty("usage_type").GetString();
   if(labels.GetProperty("unit").GetString()!="tokens"||type is not ("total_tokens" or "input_tokens" or "output_tokens" or "cached_tokens"))continue;
   var step=item.TryGetProperty("step",out var interval)?interval.GetInt64():86400;if(step!=86400)throw new JsonException();
   totals.TryAdd(type,0);
   foreach(var point in item.GetProperty("points").EnumerateArray()){
    var bucket=checked(point.GetProperty("timestamp").GetInt64()-step*1000);if(bucket<start||bucket>=end)continue;
    if(!decimal.TryParse(point.GetProperty("value").ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||value<0||value>long.MaxValue||decimal.Truncate(value)!=value)throw new JsonException();
    totals[type]=checked(totals[type]+(long)value);
   }
  }
  if(series.GetArrayLength()==0)return new(0,0);
  if(!totals.TryGetValue("total_tokens",out var total))throw new JsonException();
  var input=totals.GetValueOrDefault("input_tokens");var output=totals.GetValueOrDefault("output_tokens");var cached=totals.GetValueOrDefault("cached_tokens");
  if(checked(input+output)!=total||cached>input)throw new JsonException();
  return new(input-cached,output,cached);
 }
 private static bool IsSourceError(Exception error)=>error is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentOutOfRangeException or OverflowException;
 private static void CheckSuccess(JsonElement value){if(value.TryGetProperty("success",out var flag)&&flag.ValueKind==JsonValueKind.False)throw new JsonException();}
 private async Task<JsonDocument> Query(string url,Dictionary<string,string>? form,string header,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(form is null?HttpMethod.Get:HttpMethod.Post,url);
  request.Headers.Add("Cookie",header);request.Headers.Add("Origin","https://platform.qianwenai.com");request.Headers.Referrer=new("https://platform.qianwenai.com/home/analytics/token-plan/individual");
  if(form!=null)request.Content=new FormUrlEncodedContent(form);
  using var response=await client.SendAsync(request,ct);if((int)response.StatusCode is 401 or 403 or 302)throw new LoginException();response.EnsureSuccessStatusCode();return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
 }
 private sealed class LoginException:HttpRequestException{}
 public void Dispose()=>client.Dispose();
}
