using System.Text;using System.Text.Json;using TokNotch.Infrastructure.Usage;using TokNotch.Core.Models;using ZstdSharp;using System.Net;
var root=Path.GetFullPath("artifacts/fixtures");Directory.CreateDirectory(root);var codex=Path.Combine(root,".codex","sessions");var archives=Path.Combine(root,".codex","archived_sessions");var dsh=Path.Combine(root,".dsh","sessions");Directory.CreateDirectory(codex);Directory.CreateDirectory(archives);Directory.CreateDirectory(dsh);var now=DateTimeOffset.Now;var stamp=now.ToString("O");int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
string Event(long input,long output,long cache,long reason)=>JsonSerializer.Serialize(new{timestamp=stamp,type="event_msg",payload=new{type="token_count",info=new{total_token_usage=new{input_tokens=input,output_tokens=output,cached_input_tokens=cache,reasoning_output_tokens=reason}}}});
var a=Event(100,20,50,5);var b=Event(150,40,60,10);File.WriteAllText(Path.Combine(codex,"one.jsonl"),a+"\n"+a+"\n"+b+"\n{unfinished");File.WriteAllText(Path.Combine(archives,"copy.jsonl"),a+"\n"+b+"\n");
var usage=new{inputTokens=10,outputTokens=20,cacheReadTokens=30,cacheWriteTokens=40,totalTokens=100};var drow=JsonSerializer.Serialize(new{type="assistant/message",time=now.ToUnixTimeMilliseconds(),data=new{usage,stream=new[]{new{chunk=new{usage}}}}});using(var file=File.Create(Path.Combine(dsh,"session.v4.jsonl.zstd")))using(var stream=new CompressionStream(file)){var bytes=Encoding.UTF8.GetBytes(drow+"\n"+drow+"\n");stream.Write(bytes);}
var source=new LocalUsageSource(root);var data=source.Read();var c=data.Vendors.First(v=>v.Provider==Provider.OpenAI);var d=data.Vendors.First(v=>v.Provider==Provider.Dsh);
Check(c.Today.Tokens.Total==190,"Codex cumulative events and copied archive deduplicated");Check(c.Today.Tokens.Input==90&&c.Today.Tokens.CacheRead==60&&c.Today.Tokens.Output==30&&c.Today.Tokens.Reasoning==10,"Codex cache and reasoning not counted twice");Check(d.Today.Tokens.Total==100,"DSH distinct cache buckets; stream and duplicate excluded");Check(source.Read().TotalTokens==290,"unchanged cached reread stable");
File.AppendAllText(Path.Combine(codex,"one.jsonl"),"\n"+Event(180,50,70,12)+"\n");Check(source.Read().Vendors.First(v=>v.Provider==Provider.OpenAI).Today.Tokens.Total==230,"changed log refreshed after partial line");using(var held=new FileStream(Path.Combine(codex,"one.jsonl"),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)){Check(source.Read().TodayTokens==330,"active writer readable");}
var handler=new Handler();using var client=new HttpClient(handler);using var kimi=new KimiBalanceSource(client);Check((await kimi.ReadAsync(default)).Balance==null,"unconfigured balance unknown");kimi.Configure("fixture-key");var balance=await kimi.ReadAsync(default);Check(balance.Balance==12.5m&&balance.Cash==-1m&&balance.Voucher==13.5m,"Kimi available cash voucher; negative cash retained");handler.Status=HttpStatusCode.TooManyRequests;kimi.RequestRefresh();var stale=await kimi.ReadAsync(default);Check(stale.Balance==12.5m&&stale.SourceStatus!.Contains("已过期"),"Kimi failure preserves last balance and marks stale");var requests=handler.Requests;await kimi.ReadAsync(default);Check(handler.Requests==requests,"Kimi five minute polling gate");handler.Status=HttpStatusCode.OK;handler.Body="{\"code\":1,\"status\":false}";kimi.Configure("fixture-key");Check((await kimi.ReadAsync(default)).Balance==null,"Kimi API failure is unknown, not zero");
File.AppendAllText(Path.Combine(codex,"one.jsonl"),Event(10,5,2,1)+"\n");Check(source.Read().Vendors.First(v=>v.Provider==Provider.OpenAI).Today.Tokens.Total==245,"Codex counter reset adds new period only");
File.WriteAllBytes(Path.Combine(dsh,"session.v4.jsonl.zstd"),new byte[]{1,2,3});var staleLogs=source.Read().Vendors.First(v=>v.Provider==Provider.Dsh);Check(staleLogs.Today.Tokens.Total==100&&staleLogs.SourceStatus!.Contains("保留"),"Damaged compression retains previous usage");Console.WriteLine($"{checks} data integration checks passed");
await SettingsAndMimoTests.Run(root);
await BalanceTests.Run(root);
await DeepSeekTests.Run(root);
await RingTests.Run(root);
RingChoiceTests.Run(root);
WindowPreferenceTests.Run(root);
GlassFrameRateTests.Run(root);
await ApiKeyTests.Run(root);
await RefreshTests.Run(root);
await AuthenticationTests.Run(root);
sealed class Handler:HttpMessageHandler
{
 public HttpStatusCode Status=HttpStatusCode.OK;public int Requests;public string Body="{\"code\":0,\"status\":true,\"data\":{\"available_balance\":12.5,\"cash_balance\":-1,\"voucher_balance\":13.5}}";
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Requests++;ct.ThrowIfCancellationRequested();if(request.RequestUri!.AbsoluteUri!="https://api.moonshot.cn/v1/users/me/balance"||request.Headers.Authorization?.Parameter!="fixture-key")throw new Exception("endpoint or authorization");return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(Body)});}
}
