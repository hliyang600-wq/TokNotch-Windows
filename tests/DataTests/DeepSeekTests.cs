using System.Net;
using System.Text;
using System.Text.Json;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Usage;
using TokNotch.Infrastructure.Settings;
using ZstdSharp;
internal static class DeepSeekTests
{
 public static async Task Run(string root)
 {
  int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
  var home=Path.Combine(root,"deepseek");var dir=Path.Combine(home,".dsh","sessions");Directory.CreateDirectory(dir);long stamp=DateTimeOffset.Now.ToUnixTimeMilliseconds();
  string Usage(long n)=>JsonSerializer.Serialize(new{type="assistant/message",time=stamp++,data=new{usage=new{inputTokens=n,outputTokens=n,cacheReadTokens=n*2,cacheWriteTokens=0,totalTokens=n*4}}});
  string Context(string provider,string model="deepseek-v4-pro")=>JsonSerializer.Serialize(new{type="request/context",data=new{provider,model}});
  var api=Usage(100);var lines=new[]{Usage(7),Context("deepseek-account"),Usage(1000),Context("deepseek-official"),api,api,Context("xiaomi","mimo-v2.6-pro"),Usage(2000),Context("xiaomi"),Usage(3000),Context("deepseek"),Usage(50)};
  using(var f=File.Create(Path.Combine(dir,"session.v4.jsonl.zstd")))using(var z=new CompressionStream(f)){z.Write(Encoding.UTF8.GetBytes(string.Join("\n",lines)));}
  var logs=new LocalUsageSource(home).Read();var local=logs.Vendors.Single(v=>v.Provider==Provider.DeepSeek);
  Check(local.Today.Tokens.Total==600&&local.AllTime.Tokens.CacheRead==300,"DeepSeek API attribution excludes account-login, Xiaomi and missing context; duplicate excluded");
  Check(logs.Vendors.Single(v=>v.Provider==Provider.Dsh).Today.Tokens.Total==24628,"DSH original multi-provider total stays unchanged");Check(logs.TotalTokens==24628,"derived DeepSeek view does not duplicate overall DSH totals");
  var preferences=new DisplayPreferences(new[]{Provider.DeepSeek,Provider.Dsh,Provider.Mimo},DisplayPreferences.Default.Metrics);var store=new DisplayPreferencesStore(home);store.Save(preferences);Check(store.Load().Providers[0]==Provider.DeepSeek&&ProviderCatalog.Available.Count==5,"fifth provider selection survives restart");
  var handler=new Handler();using var source=new DeepSeekApiSource(new HttpClient(handler));var result=await source.ReadAsync(local,default);
  Check(result.Balance==null&&result.Today.Tokens.Total==600&&result.TokenUsageAvailable&&handler.Requests==0,"unconfigured API balance stays unknown while local tokens remain available");
  source.Configure("fixture-key");result=await source.ReadAsync(local,default);Check(result.Balance==110m&&result.Cash==100m&&result.Voucher==10m&&result.Currency=="CNY"&&result.Today.Tokens.Total==600,"official balance parsed alongside local tokens");
  var count=handler.Requests;await source.ReadAsync(local,default);Check(handler.Requests==count,"DeepSeek five-minute polling gate");
  handler.Status=HttpStatusCode.Unauthorized;source.RequestRefresh();result=await source.ReadAsync(local,default);Check(result.Balance==110m&&result.BalanceStatus!.Contains("已过期")&&result.Today.Tokens.Total==600,"DeepSeek auth failure preserves stale balance and current local tokens");
  source.Configure("fixture-key");result=await source.ReadAsync(local,default);Check(result.Balance==null,"new connection cannot reuse previous account balance");
  handler.Status=HttpStatusCode.OK;handler.Body="{\"is_available\":false,\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"-1.25\",\"granted_balance\":\"0\",\"topped_up_balance\":\"-1.25\"}]}";source.RequestRefresh();result=await source.ReadAsync(local,default);Check(result.Balance==-1.25m&&result.Currency=="USD","insufficient balance and USD negative amount are valid results");
  var absent=local with {Detection=DetectionState.NotDetected};result=await source.ReadAsync(absent,default);Check(result.Detection==DetectionState.Ready&&!result.TokenUsageAvailable&&result.BalanceStatus!.Contains("Token 历史无接口"),"available balance never fabricates zero historical tokens");
  handler.Body="{\"is_available\":false,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"0\",\"granted_balance\":\"0\",\"topped_up_balance\":\"0\"}]}";source.RequestRefresh();Check((await source.ReadAsync(local,default)).Balance==0m,"DeepSeek known zero remains distinct from missing balance");
  source.Configure("fixture-key");handler.Body="{\"is_available\":true,\"balance_infos\":[]}";Check((await source.ReadAsync(local,default)).Balance==null,"missing currency balance remains unknown");
  bool rejected=false;try{source.Configure("fixture\r\nInjected: value");}catch(ArgumentException){rejected=true;}Check(rejected,"DeepSeek API key header injection rejected");
  Console.WriteLine($"{checks} DeepSeek checks passed");
 }
 private sealed class Handler:HttpMessageHandler
 {
  public int Requests;public HttpStatusCode Status=HttpStatusCode.OK;public string Body="{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"110\",\"granted_balance\":\"10\",\"topped_up_balance\":\"100\"}]}";
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();Requests++;if(request.Method!=HttpMethod.Get||request.RequestUri!.AbsoluteUri!="https://api.deepseek.com/user/balance"||request.Headers.Authorization?.Parameter!="fixture-key")throw new Exception("Unexpected DeepSeek credential destination or billable request");return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(Body)});}
 }
}
