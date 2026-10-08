using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TokNotch.Core.Interaction;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Usage;
using TokNotch.UI.ViewModels;
using ZstdSharp;
namespace TokNotch.UI;

/// <summary>Offline diagnostic through the actual ring event and App refresh gate, with counted HTTP responses.</summary>
internal static class ManualRefreshValidation
{
 internal static async Task RunAsync(App app,IslandWindow island,IslandViewModel model,Func<Task> automatic,Func<Task<string>> manual)
 {
  var output=Path.Combine(ApplicationPaths.ArtifactsDirectory,"manual-refresh");
  Directory.CreateDirectory(output);var checks=new List<string>();
  void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
  var home=Path.Combine(output,"fixture-home");
  var codex=Path.Combine(home,".codex","sessions","fixture.jsonl");var dsh=Path.Combine(home,".dsh","sessions","fixture.zstd");
  Directory.CreateDirectory(Path.GetDirectoryName(codex)!);Directory.CreateDirectory(Path.GetDirectoryName(dsh)!);
  var now=DateTimeOffset.Now;
  void WriteLogs(int input,int used,int dshInput)
  {
   var log=JsonSerializer.Serialize(new{timestamp=now.ToString("O"),type="event_msg",payload=new{type="token_count",info=new{total_token_usage=new{input_tokens=input,output_tokens=20}},rate_limits=new{primary=new{window_minutes=300,used_percent=used},secondary=new{window_minutes=10080,used_percent=used}}}});
   File.WriteAllText(codex,log+"\n");
   var context=JsonSerializer.Serialize(new{type="request/context",data=new{provider="deepseek",model="deepseek-chat"}});
   var row=JsonSerializer.Serialize(new{type="assistant/message",time=now.ToUnixTimeMilliseconds(),data=new{usage=new{inputTokens=dshInput,outputTokens=20}}});
   using var file=File.Create(dsh);using var stream=new CompressionStream(file);stream.Write(Encoding.UTF8.GetBytes(context+"\n"+row+"\n"));
  }
  WriteLogs(120,10,100);
  var local=new LocalUsageSource(home);var handler=new FixtureHandler();
  var kimi=new KimiBalanceSource(new HttpClient(handler,false)){Interval=TimeSpan.FromHours(1)};
  var mimo=new MimoUsageSource(new HttpClient(handler,false)){Interval=TimeSpan.FromHours(1)};
  var deep=new DeepSeekApiSource(new HttpClient(handler,false)){Interval=TimeSpan.FromHours(1)};
  kimi.Configure("fixture-key");deep.Configure("fixture-key");mimo.Configure("api-platform_serviceToken=fixture; api-platform_ph=fixture");
  void Replace(string name,object value){var field=typeof(App).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!;if(field.GetValue(app) is IDisposable original)original.Dispose();field.SetValue(app,value);}
  Replace("logs",local);Replace("kimi",kimi);Replace("mimo",mimo);Replace("deepSeek",deep);
  model.Configure(new(DisplayPreferences.Default.Providers,DisplayPreferences.Default.Metrics,expansion:ExpansionMode.AlwaysExpanded,glassEnabled:false,window:new(Animation:AnimationMode.Off)));
  model.Select(Provider.OpenAI);island.ApplyPreferences(model.Preferences);
  var ring=(Button)island.FindName("RingRefreshButton");var caption=(TextBlock)island.FindName("RingCaption");
  ProviderUsageSnapshot Data(Provider p)=>model.Snapshot!.Vendors.Single(v=>v.Provider==p);
  async Task Click()
  {
   ring.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(!ring.IsEnabled,"actual ring click waits for refresh and blocks repeat clicks");
   var deadline=DateTimeOffset.Now+TimeSpan.FromSeconds(10);
   while(!ring.IsEnabled&&DateTimeOffset.Now<deadline)await Task.Delay(10);
   Check(ring.IsEnabled,"ring button is restored when source reads finish");
  }
  await automatic();Check(handler.Requests.Count==5,"initial refresh sends DeepSeek 1 + Kimi 1 + MiMo 3 HTTP requests");
  Check(Data(Provider.OpenAI).Today.Tokens.Total==140&&Data(Provider.Dsh).Today.Tokens.Total==120,"initial fixture logs reach the model");
  await automatic();Check(handler.Requests.Count==5,"automatic refresh retains the one-hour API cache");
  var metadata=new FileInfo(codex);long length=metadata.Length;var modified=metadata.LastWriteTimeUtc;
  WriteLogs(220,20,200);File.SetLastWriteTimeUtc(codex,modified);
  Check(new FileInfo(codex).Length==length,"rewritten Codex log has identical length and modification time");
  await automatic();Check(Data(Provider.OpenAI).Today.Tokens.Total==140,"automatic log reads retain the inexpensive metadata cache");
  handler.Round=2;await Click();
  Check(handler.Requests.Count==10,"one actual ring click bypasses every API interval and sends five new requests");
  Check(Data(Provider.OpenAI).Today.Tokens.Total==240&&Math.Abs(Data(Provider.OpenAI).Ring.Fraction!.Value-.8)<.001,"manual click rereads even identical-metadata logs and updates Codex tokens and quota");
  Check(Data(Provider.Dsh).Today.Tokens.Total==220&&Data(Provider.DeepSeek).Today.Tokens.Total==220,"fresh DSH log also supplies attributed DeepSeek local tokens");
  Check(Data(Provider.DeepSeek).Balance==20&&Data(Provider.Dsh).Balance==20&&Data(Provider.Kimi).Balance==20&&Data(Provider.Mimo).Balance==20,"changed responses reach all balances; DSH shares the single DeepSeek request");
  Check(Data(Provider.Mimo).AllTime.Tokens.Total==240,"new MiMo API token response reaches the model");
  model.Select(Provider.Mimo);await Task.Delay(20);
  Check(Data(Provider.Mimo).Today.Tokens.Total==240&&model.FirstMetricLabel=="今日 UTC"&&model.SecondMetricLabel=="本月 UTC","MiMo today's fresh API usage and explicit UTC row labels reach the UI");
  model.Select(Provider.OpenAI);
  Check(ring.ToolTip is null,"refresh introduces no hover popup");
  var gate=(SemaphoreSlim)typeof(App).GetField("refreshGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(app)!;
  await gate.WaitAsync();int before=handler.Requests.Count;
  try{
   ring.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(30);
   Check(caption.Text=="刷新中…"&&!ring.IsEnabled&&handler.Requests.Count==before,"queued manual refresh shows its real pending state");
   foreach(var source in new object[]{kimi,mimo,deep})Check((DateTimeOffset)source.GetType().GetField("next",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(source)!>DateTimeOffset.Now,"force reset happens only after acquiring the refresh gate: "+source.GetType().Name);
  }finally{gate.Release();}
  for(int i=0;i<1000&&!ring.IsEnabled;i++)await Task.Delay(10);
  Check(ring.IsEnabled&&handler.Requests.Count==before+5,"queued click still performs its own five fresh reads");
  var successful=model.Snapshot!.LastSuccessAt;handler.FailAll=true;
  var message=await manual();await Task.Delay(20);
  Check(!message.StartsWith("已刷新")&&model.Snapshot!.RefreshState==RefreshState.Failed&&caption.Text!="部分未更新","remote HTTP failures never report full success or falsely mark successfully refreshed Codex logs as failed");
  model.Select(Provider.Kimi);await Task.Delay(20);Check(caption.Text=="部分未更新","the failed selected provider still reports its failure");model.Select(Provider.OpenAI);
  Check(model.Snapshot.LastSuccessAt==successful&&Data(Provider.Kimi).Balance==20&&Data(Provider.DeepSeek).Balance==20&&Data(Provider.Mimo).AllTime.Tokens.Total==240,"failed refresh retains money, tokens and last successful timestamp");
  Check(new[]{Provider.Kimi,Provider.Mimo,Provider.DeepSeek,Provider.Dsh}.All(p=>Data(p).RefreshFailed),"individual source failures propagate without matching status text");
  handler.FailAll=false;handler.Round=3;handler.FailMimoBalance=true;await Click();
  Check(Data(Provider.Mimo).AllTime.Tokens.Total==360&&Data(Provider.Mimo).Balance==20&&Data(Provider.Mimo).RefreshFailed,"MiMo balance failure keeps fresh usage and marks the preserved balance stale");
  handler.FailMimoBalance=false;File.WriteAllBytes(dsh,new byte[]{1,2,3});await Click();
  Check(Data(Provider.Dsh).Today.Tokens.Total==220&&Data(Provider.Dsh).RefreshFailed&&model.Snapshot!.RefreshState==RefreshState.Failed,"forced parsing failure preserves previously read log data and reports failure");
  WriteLogs(320,30,300);handler.Round=4;await Click();await Task.Delay(20);
  Check(model.Snapshot!.RefreshState==RefreshState.Ready&&model.Snapshot.Vendors.All(v=>!v.RefreshFailed)&&caption.Text!="部分未更新"&&Data(Provider.Mimo).Balance==40,"the next successful click clears failure state and publishes new responses");
  Check(Data(Provider.OpenAI).Today.Tokens.Total==340&&Data(Provider.Dsh).Today.Tokens.Total==320,"recovery rereads both local sources");
  using(var locked=new FileStream(codex,FileMode.Open,FileAccess.Read,FileShare.None)){
   await manual();Check(Data(Provider.OpenAI).RefreshFailed&&Data(Provider.OpenAI).Today.Tokens.Total==340,"temporarily locked unchanged log preserves tokens and reports a failed forced read");
   await automatic();Check(Data(Provider.OpenAI).RefreshFailed&&model.Snapshot!.RefreshState==RefreshState.Failed,"automatic refresh retries a failed unchanged log instead of relabelling cached data as fresh");
  }
  await automatic();Check(!Data(Provider.OpenAI).RefreshFailed&&model.Snapshot!.RefreshState==RefreshState.Ready,"unlocking the log lets the next scheduled read recover");
  kimi.Configure("");deep.Configure("");mimo.Disconnect();await Click();
  Check(model.Snapshot!.RefreshState==RefreshState.Ready&&Data(Provider.Kimi).Balance is null&&Data(Provider.Mimo).Balance is null,"unconnected providers remain unknown without being falsely marked as request failures");
  var qwen=new QwenPlanSource(new HttpClient(handler,false)){Interval=TimeSpan.FromHours(1)};Replace("qwen",qwen);qwen.Configure("session=fixture-cookie");
  model.Configure(new(new[]{Provider.Qwen,Provider.OpenAI,Provider.Dsh},DisplayPreferences.Default.Metrics,glassEnabled:false,window:new(Animation:AnimationMode.Off)));model.Select(Provider.Qwen);island.ApplyPreferences(model.Preferences);
  handler.Round=5;await automatic();await Task.Delay(20);
  Check(Data(Provider.Qwen).Ring.Fraction==.5&&model.RingFraction==.5&&model.FirstMetricValue==600&&model.SecondMetricValue==Data(Provider.Qwen).Ring.ResetsAt!.Value.ToUnixTimeSeconds()&&model.FirstMetricLabel=="今日 Token"&&model.SecondMetricFormat=="ResetTime"&&!model.ThirdMetricVisible,"Qwen today's actual tokens and reset time reach customizable rows with monthly remaining in ring");
  before=handler.Requests.Count;handler.Round=6;await Click();await Task.Delay(20);
  Check(handler.Requests.Count==before+3&&Data(Provider.Qwen).Today.Tokens.Total==720&&Math.Abs(model.RingFraction!.Value-.4)<.0001&&model.CenterLabel=="月剩余额度","actual Qwen ring click bypasses cache and rereads both daily tokens and monthly quota");
  mimo.Configure("api-platform_serviceToken=fixture; api-platform_ph=fixture");handler.FailMimoBalance=true;
  message=await manual();await Task.Delay(20);
  Check(Data(Provider.Mimo).RefreshFailed&&!Data(Provider.Qwen).RefreshFailed&&caption.Text=="月剩余额度"&&model.Snapshot!.RefreshState==RefreshState.Failed,"unselected MiMo failure never overwrites the successful Qwen ring caption");
  Check(message.Contains("小米 MiMo")&&!message.Contains("千问"),"overall refresh summary names the actual failed platform");
  Check(model.RingTooltip.Contains(Data(Provider.Qwen).SourceStatus!)&&!model.RingTooltip.Contains("未更新："),"successful Qwen accessibility status contains its own fresh source details");
  model.Select(Provider.Mimo);await Task.Delay(20);Check(caption.Text=="部分未更新","switching to the failed platform immediately shows its own failure");
  model.Select(Provider.Qwen);handler.FailMimoBalance=false;await automatic();await Task.Delay(20);
  Check(model.Snapshot!.RefreshState==RefreshState.Failed&&caption.Text=="月剩余额度","cached failure of another source remains isolated on automatic refresh");
  await Click();Check(model.Snapshot!.RefreshState==RefreshState.Ready&&caption.Text=="月剩余额度","manual recovery clears the aggregate failure and preserves the correct Qwen caption");
  var recovered=model.Snapshot;model.Apply(new(recovered.GeneratedAt,recovered.LastSuccessAt,recovered.BucketTimeZone,false,RefreshState.Failed,recovered.Vendors,recovered.Models,"刷新失败 · 显示上次数据"));await Task.Delay(20);
  Check(caption.Text=="部分未更新"&&model.RingTooltip.Contains("刷新失败"),"unexpected whole-refresh failure is not hidden by previously successful provider data");
  model.Apply(recovered);
  await File.WriteAllLinesAsync(Path.Combine(output,"checks.txt"),checks);
  await File.WriteAllTextAsync(Path.Combine(output,"http-requests.json"),JsonSerializer.Serialize(handler.Requests,new JsonSerializerOptions{WriteIndented=true}));
 }
 private sealed class FixtureHandler:HttpMessageHandler
 {
  public int Round=1;public bool FailAll,FailMimoBalance;public List<object> Requests=new();
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   var uri=request.RequestUri!;Requests.Add(new{Sequence=Requests.Count+1,Round,Method=request.Method.Method,Endpoint=uri.GetLeftPart(UriPartial.Path)});
   await Task.Delay(5,ct);
   if(FailAll||FailMimoBalance&&uri.AbsolutePath=="/api/v1/balance")return new(HttpStatusCode.ServiceUnavailable);
   string body;
   if(uri.Host=="platform-home.qianwenai.com"&&uri.AbsolutePath=="/tool/user/info.json")body="{\"data\":{\"secToken\":\"fixture-sec\"}}";
   else if(uri.Host=="cs-data.qianwenai.com"&&uri.AbsolutePath=="/data/api.json"){
    var form=(await request.Content!.ReadAsStringAsync(ct)).Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>Uri.UnescapeDataString(p[1].Replace('+',' ')));using var param=JsonDocument.Parse(form["params"]);
    object data;
    if(param.RootElement.GetProperty("Api").GetString()=="zeldaEasy.bailian-telemetry.platform-model.getModelMonitorDataWithOss"){
     var end=param.RootElement.GetProperty("Data").GetProperty("reqDTO").GetProperty("endTime").GetInt64();
     object Item(string type,int value)=>new{aggMethod="sum",metricName="model_usage",step=86400,labels=new{usage_type=type,unit="tokens"},points=new[]{new{timestamp=end,value}}};
     data=new{originData=new[]{Item("total_tokens",120*Round),Item("input_tokens",100*Round),Item("output_tokens",20*Round),Item("cached_tokens",40*Round)}};
    }else data=new{per1MonthPercentage=Round/10d,per1MonthResetTime=DateTimeOffset.Now.AddDays(20).ToUnixTimeMilliseconds()};
    body=JsonSerializer.Serialize(new{successResponse=true,data=new{success=true,DataV2=new{data=new{success=true,data}}}});
   }
   else if(uri.Host=="api.deepseek.com"&&uri.AbsolutePath=="/user/balance")body=JsonSerializer.Serialize(new{is_available=true,balance_infos=new[]{new{currency="CNY",total_balance=(Round*10).ToString(),topped_up_balance=(Round*10).ToString(),granted_balance="0"}}});
   else if(uri.Host=="api.moonshot.cn"&&uri.AbsolutePath=="/v1/users/me/balance")body=JsonSerializer.Serialize(new{code=0,status=true,data=new{available_balance=Round*10,cash_balance=Round*10,voucher_balance=0}});
   else if(uri.Host=="platform.xiaomimimo.com"){
    object data=uri.AbsolutePath switch{
     "/api/v1/usage"=>new{tokenUsage=new{inputToken=100*Round,outputToken=20*Round,cacheToken=0,totalToken=120*Round}},
     "/api/v1/usage/detail/list"=>new[]{new{date=DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),inputMissToken=100*Round,outputToken=20*Round,inputHitToken=0,totalToken=120*Round}},
     "/api/v1/balance"=>new{balance=10*Round,cashBalance=10*Round,giftBalance=0,currency="CNY"},
     _=>throw new Exception("Unexpected MiMo endpoint")};body=JsonSerializer.Serialize(new{code=0,data});
   }else throw new Exception("Unexpected endpoint");
   return new(HttpStatusCode.OK){Content=new StringContent(body)};
  }
 }
}
