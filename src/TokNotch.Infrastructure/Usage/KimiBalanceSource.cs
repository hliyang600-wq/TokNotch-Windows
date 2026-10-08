using System.Net.Http.Headers;
using System.Text.Json;
using TokNotch.Core.Models;
namespace TokNotch.Infrastructure.Usage;
public sealed class KimiBalanceSource : IDisposable
{
 private readonly HttpClient client; public KimiBalanceSource(HttpClient? httpClient=null)=>client=httpClient??new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};
 private string? key; private ProviderUsageSnapshot? previous;private DateTimeOffset next; private int revision;
 /// <summary>Shortest gap between two official balance queries; the tray refresh calls RequestRefresh and ignores it.</summary>
 public TimeSpan Interval {get;set;}=TimeSpan.FromMinutes(5);
 public void RequestRefresh()=>next=DateTimeOffset.MinValue;
 public void Configure(string apiKey){key=apiKey.Trim();previous=null;next=DateTimeOffset.MinValue;revision++;}
 public async Task<ProviderUsageSnapshot> ReadAsync(CancellationToken ct)
 {
  var zero=new PeriodUsage(new(0,0),null,CostStatus.Unavailable);
  ProviderUsageSnapshot Empty(string status)=>new(Provider.Kimi,"kimi","Kimi","Kimi",DetectionState.Unavailable,zero,zero,zero,zero,null,new("kimi",null,"余额")){SourceStatus=status};
  if(string.IsNullOrWhiteSpace(key))return Empty("未连接 · 托盘 → 连接 Kimi");if(DateTimeOffset.Now<next&&previous!=null)return previous;next=DateTimeOffset.Now+Interval;
  var version=revision;var activeKey=key;
  try{using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.moonshot.cn/v1/users/me/balance");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",activeKey);using var response=await client.SendAsync(request,ct);if(!response.IsSuccessStatusCode)return Failure($"Kimi HTTP {(int)response.StatusCode}");using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var root=doc.RootElement;if(root.GetProperty("code").GetInt32()!=0||root.GetProperty("status").ValueKind!=JsonValueKind.True)return Failure("Kimi 返回失败状态");var data=root.GetProperty("data");var result=Empty($"余额 CNY · {DateTimeOffset.Now:HH:mm} 更新") with {Detection=DetectionState.Ready,Balance=data.GetProperty("available_balance").GetDecimal(),Cash=data.GetProperty("cash_balance").GetDecimal(),Voucher=data.GetProperty("voucher_balance").GetDecimal()};if(version==revision)previous=result;return version==revision?result:Empty("连接已更改 · 待刷新");}
  catch(OperationCanceledException)when(!ct.IsCancellationRequested){return Failure("Kimi 请求超时");}catch(Exception e)when(e is HttpRequestException or JsonException or KeyNotFoundException or FormatException or InvalidOperationException){return Failure("Kimi 连接失败");}
  ProviderUsageSnapshot Failure(string text){ if(version!=revision)return Empty("连接已更改 · 待刷新");previous=previous?.Balance is not null ? previous with {RefreshFailed=true,SourceStatus=text+" · 上次余额（已过期）"} : Empty(text) with {RefreshFailed=true}; return previous; }
 }
 public void Dispose()=>client.Dispose();
}
