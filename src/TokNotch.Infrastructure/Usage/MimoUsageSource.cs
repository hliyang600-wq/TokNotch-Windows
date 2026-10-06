using System.Net.Http.Json;
using System.Text.Json;
using TokNotch.Core.Models;
namespace TokNotch.Infrastructure.Usage;
/// <summary>Read-only console queries verified against Xiaomi's public frontend on 2026-10-06.</summary>
public sealed class MimoUsageSource : IDisposable
{
 private readonly HttpClient client;
 private string? cookie;private ProviderUsageSnapshot? previous;private DateTimeOffset next;private int revision;
 public MimoUsageSource(HttpClient? httpClient=null)=>client=httpClient??new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(15)};
 public bool RequiresLogin {get;private set;}
 /// <summary>Shortest gap between two console queries; the tray refresh calls RequestRefresh and ignores it.</summary>
 public TimeSpan Interval {get;set;}=TimeSpan.FromMinutes(5);
 public void Disconnect(){cookie=null;previous=null;next=DateTimeOffset.MinValue;revision++;RequiresLogin=false;}
 public void Configure(string header){if(header.Contains('\r')||header.Contains('\n'))throw new ArgumentException("Cookie 只能是一行请求头");RequiresLogin=false;cookie=header.Trim();previous=null;next=DateTimeOffset.MinValue;revision++;}
 public void RequestRefresh()=>next=DateTimeOffset.MinValue;
 private static ProviderUsageSnapshot Empty(string status){var zero=new PeriodUsage(new(0,0),null,CostStatus.Unavailable);return new(Provider.Mimo,"mimo","小米 MiMo","小米 MiMo",DetectionState.Unavailable,zero,zero,zero,zero,null,new("mimo",null,"今日占本月")){SourceStatus=status};}
 public async Task<ProviderUsageSnapshot> ReadAsync(CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(cookie))return Empty("未连接 · 设置 → 数据连接 → 登录 MiMo");if(DateTimeOffset.Now<next&&previous!=null)return previous;
  var now=DateTimeOffset.Now;var version=revision;var activeCookie=cookie;next=now+Interval;
  try{
   using var history=await Query("/usage",null,activeCookie,ct);using var monthly=await Query("/usage/detail/list",new{year=now.Year,month=now.Month},activeCookie,ct);
   var aggregate=history.RootElement.GetProperty("tokenUsage");
   var all=Counts(N(aggregate,"inputToken")+N(aggregate,"batchInputToken"),N(aggregate,"outputToken")+N(aggregate,"batchOutputToken"),N(aggregate,"cacheToken")+N(aggregate,"batchCacheToken"),N(aggregate,"totalToken"),true);
   var month=new long[3];var today=new long[3];var week=new long[3];var monday=DateOnly.FromDateTime(now.Date.AddDays(-((7+(int)now.DayOfWeek-1)%7)));var localToday=DateOnly.FromDateTime(now.Date);
   if(monthly.RootElement.ValueKind!=JsonValueKind.Array)throw new JsonException();
   foreach(var row in monthly.RootElement.EnumerateArray()){
    if(!DateOnly.TryParse(row.GetProperty("date").GetString(),out var date)||date.Year!=now.Year||date.Month!=now.Month)continue;
    long[] buckets={N(row,"inputMissToken"),N(row,"outputToken"),N(row,"inputHitToken")};if(checked(buckets.Sum())!=N(row,"totalToken"))throw new JsonException();
    for(int i=0;i<3;i++){month[i]=checked(month[i]+buckets[i]);if(date==localToday)today[i]=checked(today[i]+buckets[i]);if(date>=monday&&date<=localToday)week[i]=checked(week[i]+buckets[i]);}
   }
   PeriodUsage Period(long[] a)=>new(new(a[0],a[1],a[2]),null,CostStatus.Unavailable);
   var t=Period(today);var m=Period(month);var result=new ProviderUsageSnapshot(Provider.Mimo,"mimo","小米 MiMo","小米 MiMo",DetectionState.Ready,t,Period(week),m,new(all,null,CostStatus.Unavailable),null,new("mimo",null,"可用余额 · 金额圆环")){SourceStatus=$"控制台用量 · {now:HH:mm} 更新"};
   try{
    using var balance=await Query("/balance",null,activeCookie,ct);var b=balance.RootElement;
    decimal Money(string name){var value=b.GetProperty(name);return value.ValueKind==JsonValueKind.String?decimal.Parse(value.GetString()!,System.Globalization.CultureInfo.InvariantCulture):value.GetDecimal();}
    var currency=b.TryGetProperty("currency",out var c)?c.GetString():"CNY";if(currency is not ("CNY" or "USD"))throw new JsonException();
    result=result with {Balance=Money("balance"),Cash=b.TryGetProperty("cashBalance",out _)?Money("cashBalance"):null,Voucher=b.TryGetProperty("giftBalance",out _)?Money("giftBalance"):null,Currency=currency,BalanceSource="MiMo",BalanceStatus=$"MiMo 账户余额 · {now:HH:mm} 更新"};if(version==revision)RequiresLogin=false;
   }catch(OperationCanceledException)when(!ct.IsCancellationRequested){result=BalanceFailure(result,"MiMo 余额请求超时");}
   catch(Exception e)when(e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException){if(version==revision&&e is ConsoleHttpException{HttpStatus:401 or 403 or 302})RequiresLogin=true;result=BalanceFailure(result,RequiresLogin?"MiMo 登录已过期 · 设置 → 登录 MiMo":e is ConsoleHttpException h?$"MiMo 余额 HTTP {h.HttpStatus}":"MiMo 余额暂不可读");}
   if(version==revision)previous=result;return version==revision?result:previous??Empty("连接已更改 · 待刷新");
  }catch(OperationCanceledException)when(!ct.IsCancellationRequested){return Fail("MiMo 请求超时");}catch(Exception e)when(e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException){return Fail(e is ConsoleHttpException h?$"MiMo HTTP {h.HttpStatus} · 请检查登录 Cookie":"MiMo 查询失败 · 请检查 Cookie 或稍后刷新");}
  ProviderUsageSnapshot Fail(string text){if(version==revision && (text.Contains("HTTP 401")||text.Contains("HTTP 403")||text.Contains("HTTP 302"))){RequiresLogin=true;text="MiMo 登录已过期 · 设置 → 登录 MiMo";}if(version!=revision)return previous??Empty("连接已更改 · 待刷新");previous=previous is null?Empty(text):previous with {SourceStatus=text+" · 上次用量（已过期）",BalanceStatus=text+" · 上次余额（已过期）"};return previous;}
  ProviderUsageSnapshot BalanceFailure(ProviderUsageSnapshot result,string text)=>result with {Balance=previous?.Balance,Cash=previous?.Cash,Voucher=previous?.Voucher,Currency=previous?.Currency??"CNY",BalanceSource="MiMo",BalanceStatus=text+(previous?.Balance!=null?" · 上次余额（已过期）":" · —")};
 }
 private async Task<JsonDocument> Query(string path,object? body,string activeCookie,CancellationToken ct)
 {
  var uri="https://platform.xiaomimimo.com/api/v1"+path;
  // The console sends its anti-CSRF cookie as a query parameter for the POST read query.
  if(body!=null){var ph=activeCookie.Split(';').Select(v=>v.Trim()).FirstOrDefault(v=>v.StartsWith("api-platform_ph=",StringComparison.Ordinal));if(ph!=null)uri+="?api-platform_ph="+Uri.EscapeDataString(Uri.UnescapeDataString(ph[16..].Trim('"')));}
  using var request=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Post,uri);request.Headers.Add("Cookie",activeCookie);request.Headers.Add("Accept-Language","zh-CN");request.Headers.Add("Origin","https://platform.xiaomimimo.com");request.Headers.Referrer=new("https://platform.xiaomimimo.com/console/usage");
  request.Headers.Add("x-timeZone",TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id,out var iana)?iana:TimeZoneInfo.Local.Id);if(body!=null)request.Content=JsonContent.Create(body);
  using var response=await client.SendAsync(request,ct);if(!response.IsSuccessStatusCode)throw new ConsoleHttpException((int)response.StatusCode);
  using var envelope=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var root=envelope.RootElement;var code=root.GetProperty("code").GetInt32();if(code is 401 or 403)throw new ConsoleHttpException(code);if(code!=0&&code!=200)throw new JsonException();return JsonDocument.Parse(root.GetProperty("data").GetRawText());
 }
 private static long N(JsonElement e,string name){if(!e.TryGetProperty(name,out var value))return 0;long n=value.ValueKind==JsonValueKind.String?long.Parse(value.GetString()!,System.Globalization.CultureInfo.InvariantCulture):value.GetInt64();return n>=0?n:throw new JsonException();}
 private static TokenCounts Counts(long input,long output,long cached,long total,bool included){var counts=new TokenCounts(included?Math.Max(0,input-cached):input,output,cached);if(counts.Total!=total)throw new JsonException();return counts;}
 private sealed class ConsoleHttpException(int statusCode):HttpRequestException{public int HttpStatus {get;}=statusCode;}
 public void Dispose()=>client.Dispose();
}


