using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using TokNotch.Core.Models;
namespace TokNotch.Infrastructure.Usage;
/// <summary>Official read-only balance API plus explicitly attributed local request usage. No historical usage endpoint is assumed.</summary>
public sealed class DeepSeekApiSource : IDisposable
{
 private sealed record Account(decimal Total,decimal Cash,decimal Granted,string Currency,string Status);
 private readonly HttpClient client;
 private bool failed;private string? key;private Account? previous;private DateTimeOffset next;private int revision;private string status="未连接 · 设置 → DeepSeek API";
 /// <summary>Shortest gap between two official balance queries; the tray refresh calls RequestRefresh and ignores it.</summary>
 public TimeSpan Interval {get;set;}=TimeSpan.FromMinutes(5);
 public DeepSeekApiSource(HttpClient? httpClient=null)=>client=httpClient??new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};
 public void Configure(string apiKey){if(apiKey.Length>8192||apiKey.Contains('\r')||apiKey.Contains('\n'))throw new ArgumentException("API key 格式无效");key=apiKey.Trim();failed=false;previous=null;next=DateTimeOffset.MinValue;status="待查询";revision++;}
 public void RequestRefresh()=>next=DateTimeOffset.MinValue;
 public async Task<ProviderUsageSnapshot> ReadAsync(ProviderUsageSnapshot local,CancellationToken ct)
 {
  if(local.Provider!=Provider.DeepSeek)throw new ArgumentException("Expected DeepSeek local usage");
  int version=revision;
  if(!string.IsNullOrWhiteSpace(key)&&DateTimeOffset.Now>=next){
   next=DateTimeOffset.Now+Interval;var activeKey=key;
   try{
    using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.deepseek.com/user/balance");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",activeKey);
    using var response=await client.SendAsync(request,ct);
    if(!response.IsSuccessStatusCode){Fail($"DeepSeek HTTP {(int)response.StatusCode}");}
    else{
     using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var root=doc.RootElement;
     if(root.GetProperty("is_available").ValueKind is not (JsonValueKind.True or JsonValueKind.False))throw new JsonException();
     var infos=root.GetProperty("balance_infos").EnumerateArray().ToArray();
     var matches=infos.Where(v=>v.GetProperty("currency").GetString()=="CNY").ToArray();if(matches.Length==0)matches=infos.Where(v=>v.GetProperty("currency").GetString()=="USD").ToArray();if(matches.Length!=1)throw new JsonException();var b=matches[0];
     decimal Money(string name)=>decimal.Parse(b.GetProperty(name).GetString()!,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
     var result=new Account(Money("total_balance"),Money("topped_up_balance"),Money("granted_balance"),b.GetProperty("currency").GetString()!,$"DeepSeek 余额 · {DateTimeOffset.Now:HH:mm} 更新");
     if(version==revision){previous=result;status=result.Status;failed=false;}
    }
   }catch(OperationCanceledException)when(!ct.IsCancellationRequested){Fail("DeepSeek 请求超时");}
   catch(Exception e)when(e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException){Fail("DeepSeek 余额查询失败");}
  }
  bool tokens=local.Detection is DetectionState.Ready or DetectionState.DetectedNoUsage;
  return local with {RefreshFailed=local.RefreshFailed||failed,Detection=tokens?local.Detection:previous!=null?DetectionState.Ready:local.Detection,Balance=previous?.Total,Cash=previous?.Cash,Voucher=previous?.Granted,Currency=previous?.Currency??"CNY",BalanceSource="DeepSeek",TokenUsageAvailable=tokens,BalanceStatus=status+(tokens?" · Token 仅本地":" · Token 历史无接口")};
  void Fail(string text){if(version==revision){failed=true;status=text+(previous!=null?" · 上次余额（已过期）":"");}}
 }
 public void Dispose()=>client.Dispose();
}
