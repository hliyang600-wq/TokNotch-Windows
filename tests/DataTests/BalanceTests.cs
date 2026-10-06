using System.Net;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Usage;
internal static class BalanceTests
{
 public static async Task Run(string root)
 {
  int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
  var handler=new Handler();using var source=new MimoUsageSource(new HttpClient(handler));source.Configure("api-platform_serviceToken=fixture-session");
  var data=await source.ReadAsync(default);Check(data.Balance==23.45m&&data.Cash==20m&&data.Voucher==3.45m&&data.Currency=="CNY","MiMo balance is currency units without token or cent conversion");
  var zero=new PeriodUsage(new(10,20),null,CostStatus.Unavailable);var local=new ProviderUsageSnapshot(Provider.Dsh,"dsh","DSH","DSH",DetectionState.Ready,zero,zero,zero,zero,null,new("dsh",null,"可用余额 · 金额圆环"));
  var account=local with {Provider=Provider.DeepSeek,Balance=110m,Cash=100m,Voucher=10m,Currency="CNY",BalanceSource="DeepSeek",BalanceStatus="DeepSeek 余额 · 12:00 更新"};
  var attached=DshBalanceBinding.Attach(local,account);Check(attached.Balance==110m&&attached.Today.Tokens.Total==30&&attached.Provider==Provider.Dsh&&attached.BalanceSource=="DeepSeek"&&attached.BalanceStatus!.Contains("DSH 余额关联：DeepSeek API"),"DSH rings the DeepSeek API balance and preserves local usage");
  Check(DshBalanceBinding.Attach(local,account with {Balance=-1.25m,Currency="USD"}).Currency=="USD","DSH ring forwards the source currency for the USD baseline");
  var unconnected=DshBalanceBinding.Attach(local,account with {Balance=null,BalanceStatus="未连接 · 设置 → DeepSeek API"});Check(unconnected.Balance is null&&unconnected.BalanceStatus!.Contains("DSH 余额关联：DeepSeek API")&&unconnected.BalanceStatus.Contains("未连接"),"unconnected DeepSeek leaves the DSH ring unknown, never zero");
  handler.Status=HttpStatusCode.ServiceUnavailable;source.RequestRefresh();data=await source.ReadAsync(default);Check(data.Balance==23.45m&&data.Detection==DetectionState.Ready&&data.BalanceStatus!.Contains("已过期"),"balance failure retains previous amount while fresh usage remains available");
  source.Configure("api-platform_serviceToken=fixture-session");data=await source.ReadAsync(default);Check(data.Balance==null&&data.Detection==DetectionState.Ready,"first balance failure is unknown and does not discard usage");
  handler.Status=HttpStatusCode.Unauthorized;source.RequestRefresh();data=await source.ReadAsync(default);Check(source.RequiresLogin&&data.BalanceStatus!.Contains("登录已过期")&&data.Detection==DetectionState.Ready,"expired balance session prompts login while keeping local usage valid");
  handler.Status=HttpStatusCode.OK;handler.Body="{\"balance\":\"-1.25\",\"currency\":\"USD\"}";source.RequestRefresh();data=await source.ReadAsync(default);Check(data.Balance==-1.25m&&data.Currency=="USD","negative and USD balances preserved accurately");
  handler.Body="{\"cashBalance\":\"9\"}";source.Configure("api-platform_serviceToken=fixture-session");data=await source.ReadAsync(default);Check(data.Balance==null,"missing total balance does not become zero or fabricated sum");
  handler.Body="{\"balance\":\"0\",\"currency\":\"CNY\"}";source.RequestRefresh();data=await source.ReadAsync(default);Check(data.Balance==0m,"valid zero balance distinguished from unknown");
  source.Disconnect();Check((await source.ReadAsync(default)).Balance==null,"disconnect clears balance");
  Console.WriteLine($"{checks} balance checks passed");
 }
 private sealed class Handler:HttpMessageHandler
 {
  public HttpStatusCode Status=HttpStatusCode.OK;public string Body="{\"balance\":\"23.45\",\"cashBalance\":\"20\",\"giftBalance\":\"3.45\",\"currency\":\"CNY\"}";
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   if(request.RequestUri!.Host!="platform.xiaomimimo.com"||!request.Headers.GetValues("Cookie").Single().Contains("fixture-session"))throw new Exception("Unexpected cookie destination");
   if(request.RequestUri.AbsolutePath=="/api/v1/balance") {if(request.Method!=HttpMethod.Get)throw new Exception("Balance must be read only");return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent("{\"code\":0,\"data\":"+Body+"}")});}
   var payload=request.RequestUri.AbsolutePath=="/api/v1/usage"?"{\"tokenUsage\":{\"inputToken\":90,\"outputToken\":10,\"cacheToken\":20,\"totalToken\":100}}":"[]";
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"code\":0,\"data\":"+payload+"}")});
  }
 }
}
