using System.Globalization;
using System.Text.Json;
using TokNotch.Core.Models;
using ZstdSharp;
namespace TokNotch.Infrastructure.Usage;
public sealed class LocalUsageSource
{
 private record Row(string Key, DateTimeOffset Time, TokenCounts Tokens,bool DeepSeekApi=false);
 /// <summary>One rate-limit window exactly as the log recorded it; window identity comes from the duration, never from field order.</summary>
 private sealed record CodexWindow(int WindowMinutes,double UsedPercent,DateTimeOffset? ResetsAt);
 private sealed record CodexQuota(DateTimeOffset RecordedAt,IReadOnlyList<CodexWindow> Windows);
 private record Cached(long Length, DateTime Modified, List<Row> Rows,CodexQuota? Quota);
 private readonly Dictionary<string,Cached> cache = new(StringComparer.OrdinalIgnoreCase);
 private readonly string home;
 public LocalUsageSource(string? root=null) => home=root??Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
 public UsageSnapshot Read(CancellationToken ct=default)
 {
  var now=DateTimeOffset.Now; var vendors=new List<ProviderUsageSnapshot>();
  vendors.Add(ReadProvider(Provider.OpenAI,"Codex",new[]{Path.Combine(home,".codex","sessions"),Path.Combine(home,".codex","archived_sessions")},"*.jsonl",now,ct));
  vendors.Add(ReadProvider(Provider.Dsh,"DSH",new[]{Path.Combine(home,".dsh","sessions")},"*.zstd",now,ct));
  vendors.Add(ReadProvider(Provider.DeepSeek,"DeepSeek API",new[]{Path.Combine(home,".dsh","sessions")},"*.zstd",now,ct));
  return new(now,now,TimeZoneInfo.Local.Id,false,RefreshState.Ready,vendors,Array.Empty<ModelUsage>());
 }
 private ProviderUsageSnapshot ReadProvider(Provider provider,string title,string[] roots,string pattern,DateTimeOffset now,CancellationToken ct)
 {
  var rows=new List<Row>(); var files=roots.Where(Directory.Exists).SelectMany(r=>Directory.EnumerateFiles(r,pattern,SearchOption.AllDirectories)).ToArray(); int failed=0;
  foreach(var file in files) { ct.ThrowIfCancellationRequested(); var info=new FileInfo(file); if(!cache.TryGetValue(file,out var old)||old.Length!=info.Length||old.Modified!=info.LastWriteTimeUtc){try{ var parsedRows=Parse(file,provider==Provider.DeepSeek?Provider.Dsh:provider,ct,out var parsedQuota); old=new(info.Length,info.LastWriteTimeUtc,parsedRows,parsedQuota);cache[file]=old;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ZstdException){failed++;}} if(old!=null)rows.AddRange(provider==Provider.DeepSeek?old.Rows.Where(r=>r.DeepSeekApi):old.Rows); }
  var existing=new HashSet<string>(roots.Where(Directory.Exists).SelectMany(r=>Directory.EnumerateFiles(r,pattern,SearchOption.AllDirectories)),StringComparer.OrdinalIgnoreCase);
  foreach(var key in cache.Keys.Where(k=>roots.Any(r=>k.StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))&&!existing.Contains(k)).ToArray()) cache.Remove(key);
  var distinct=rows.DistinctBy(r=>r.Key).ToArray();var today=now.Date;var month=new DateTime(today.Year,today.Month,1);var week=today.AddDays(-((7+(int)today.DayOfWeek-1)%7));
  PeriodUsage Sum(Func<Row,bool> filter){long a=0,b=0,c=0,d=0,e=0;foreach(var row in distinct.Where(filter)){a=checked(a+row.Tokens.Input);b=checked(b+row.Tokens.Output);c=checked(c+row.Tokens.CacheRead);d=checked(d+row.Tokens.CacheWrite);e=checked(e+row.Tokens.Reasoning);}return new(new(a,b,c,d,e),null,CostStatus.Unavailable);}
  var t=Sum(r=>r.Time.LocalDateTime.Date==today);var m=Sum(r=>r.Time.LocalDateTime>=month&&r.Time<=now);var all=Sum(_=>true);
  var quota=provider==Provider.OpenAI?LatestQuota(files):null;
  var ring=Ring(provider,title,quota,now);
  if(provider!=Provider.OpenAI)return new(provider,title.ToLowerInvariant(),title,title,files.Length==0||provider==Provider.DeepSeek&&distinct.Length==0?DetectionState.NotDetected:failed==files.Length && distinct.Length==0?DetectionState.Unavailable:distinct.Length==0?DetectionState.DetectedNoUsage:DetectionState.Ready,t,Sum(r=>r.Time.LocalDateTime>=week&&r.Time<=now),m,all,null,ring){TokenUsageParent=provider==Provider.DeepSeek?Provider.Dsh:null,SourceStatus=failed>0?$"{failed} 个日志暂不可读 · 保留已读数据":provider==Provider.DeepSeek?$"本地 DeepSeek API 会话 · 非账户全量 · {now:HH:mm} 更新":$"本地日志 {files.Length} 个 · {now:HH:mm:ss} 更新"};
  var status=files.Length==0?"未找到 Codex 会话日志":failed>0?$"{failed} 个日志暂不可读 · 保留已读数据":quota is null?$"本地日志 {files.Length} 个 · 无额度记录 · {now:HH:mm:ss} 更新":$"本地日志 {files.Length} 个 · 额度为 {quota.RecordedAt.LocalDateTime:MM-dd HH:mm} 快照";
  var inner=Inner(quota,now);
  if(ring.ResetElapsed||inner.ResetElapsed)status+=" · 已过重置时间，窗口已过期，显示上次记录";
  return new(provider,title.ToLowerInvariant(),title,title,files.Length==0?DetectionState.NotDetected:failed==files.Length&&distinct.Length==0?DetectionState.Unavailable:distinct.Length==0?DetectionState.DetectedNoUsage:DetectionState.Ready,t,Sum(r=>r.Time.LocalDateTime>=week&&r.Time<=now),m,all,null,ring){InnerRing=inner,SourceStatus=status};
 }
 /// <summary>Codex quota rings stay percentages. The five-hour and weekly windows are matched by their published duration.</summary>
 private static RingSnapshot Ring(Provider provider,string title,CodexQuota? quota,DateTimeOffset now)
 {
  if(provider!=Provider.OpenAI)return new(title.ToLowerInvariant(),null,"可用余额 · 金额圆环");
  var five=Pick(quota,300,60,2880);
  return new("codex.5h",RingMath.RemainingFraction(five?.UsedPercent),five is null?"五小时额度：无记录":"五小时剩余额度"){WindowLabel="5h",RecordedAt=quota?.RecordedAt,ResetsAt=five?.ResetsAt,ResetElapsed=five?.ResetsAt is {} reset&&now>=reset};
 }
 private static RingSnapshot Inner(CodexQuota? quota,DateTimeOffset now)
 {
  var weekly=Pick(quota,10080,2881,int.MaxValue);
  return new("codex.week",RingMath.RemainingFraction(weekly?.UsedPercent),weekly is null?"一周额度：无记录":"一周剩余额度"){WindowLabel="周",RecordedAt=quota?.RecordedAt,ResetsAt=weekly?.ResetsAt,ResetElapsed=weekly?.ResetsAt is {} reset&&now>=reset};
 }
 private static CodexWindow? Pick(CodexQuota? quota,int expected,int minimum,int maximum)
 {
  if(quota is null)return null;
  var exact=quota.Windows.FirstOrDefault(w=>w.WindowMinutes==expected);if(exact is not null)return exact;
  var candidates=quota.Windows.Where(w=>w.WindowMinutes>=minimum&&w.WindowMinutes<=maximum).OrderBy(w=>Math.Abs(w.WindowMinutes-expected)).ToArray();
  return candidates.Length>0?candidates[0]:null;
 }
 /// <summary>Latest snapshot across every readable log; an older file never overwrites a newer record.</summary>
 private CodexQuota? LatestQuota(IEnumerable<string> files)
 {
  CodexQuota? latest=null;
  foreach(var file in files)if(cache.TryGetValue(file,out var cached)&&cached.Quota is {} quota&&(latest is null||quota.RecordedAt>latest.RecordedAt))latest=quota;
  return latest;
 }
 private static long N(JsonElement e,string name)=>e.TryGetProperty(name,out var n)&&n.ValueKind==JsonValueKind.Number&&n.TryGetInt64(out var value)?Math.Max(0,value):0;
 private static List<Row> Parse(string path,Provider provider,CancellationToken ct,out CodexQuota? quota)
 {
  quota=null;var rows=new List<Row>(); using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
  using Stream stream=provider==Provider.Dsh?new DecompressionStream(file):file;using var reader=new StreamReader(stream);long[] previous=new long[5];bool deepSeekApi=false;
  while(reader.ReadLine() is {} line){ct.ThrowIfCancellationRequested();
   if(provider==Provider.Dsh){
    if(line.Contains("\"request/context\"",StringComparison.Ordinal)){try{using var context=JsonDocument.Parse(line);if(context.RootElement.GetProperty("type").GetString()=="request/context"){var data=context.RootElement.GetProperty("data");var route=data.GetProperty("provider").GetString();var model=data.GetProperty("model").GetString();deepSeekApi=(route is "deepseek" or "deepseek-official")&&model?.StartsWith("deepseek-",StringComparison.Ordinal)==true;}}catch(JsonException){deepSeekApi=false;}catch(KeyNotFoundException){deepSeekApi=false;}catch(InvalidOperationException){deepSeekApi=false;}continue;}
    if(!line.Contains("\"usage\"",StringComparison.Ordinal))continue;
   }
   else if(!line.Contains("\"token_count\"",StringComparison.Ordinal)&&!line.Contains("\"rate_limits\"",StringComparison.Ordinal))continue;
   try { using var doc=JsonDocument.Parse(line);var root=doc.RootElement; DateTimeOffset time;TokenCounts tokens;string key;
    if(provider==Provider.Dsh){if(root.GetProperty("type").GetString()!="assistant/message"||!root.GetProperty("data").TryGetProperty("usage",out var u))continue;time=DateTimeOffset.FromUnixTimeMilliseconds(root.GetProperty("time").GetInt64());tokens=new(N(u,"inputTokens"),N(u,"outputTokens"),N(u,"cacheReadTokens"),N(u,"cacheWriteTokens"));key="dsh:"+time.ToUnixTimeMilliseconds()+":"+u.GetRawText();}
    else{var payload=root.GetProperty("payload");time=root.GetProperty("timestamp").GetDateTimeOffset();ReadQuota(payload,time,ref quota);if(payload.GetProperty("type").GetString()!="token_count"||!payload.TryGetProperty("info",out var inf)||inf.ValueKind!=JsonValueKind.Object||!inf.TryGetProperty("total_token_usage",out var u))continue;long[] total={N(u,"input_tokens"),N(u,"output_tokens"),N(u,"cached_input_tokens"),N(u,"cache_write_input_tokens"),N(u,"reasoning_output_tokens")};var delta=total.Select((v,i)=>v>=previous[i]?v-previous[i]:v).ToArray();previous=total;tokens=new(Math.Max(0,delta[0]-delta[2]-delta[3]),Math.Max(0,delta[1]-delta[4]),delta[2],delta[3],delta[4]);key="codex:"+time.ToUnixTimeMilliseconds()+":"+string.Join(",",total);}
    if(tokens.Total>0)rows.Add(new(key,time,tokens,deepSeekApi));
   }catch(JsonException){/* A writer may not have finished its last line yet. */}catch(KeyNotFoundException){}catch(InvalidOperationException){}
  } return rows;
 }
 /// <summary>payload.rate_limits.primary/secondary carry used_percent, window_minutes and resets_at (unix seconds).</summary>
 private static void ReadQuota(JsonElement payload,DateTimeOffset time,ref CodexQuota? quota)
 {
  if(payload.ValueKind!=JsonValueKind.Object||!payload.TryGetProperty("rate_limits",out var limits)||limits.ValueKind!=JsonValueKind.Object)return;
  if(quota is not null&&time<=quota.RecordedAt)return;
  var windows=new List<CodexWindow>(2);
  foreach(var name in new[]{"primary","secondary"})
  {
   if(!limits.TryGetProperty(name,out var window)||window.ValueKind!=JsonValueKind.Object)continue;
   if(!Number(window,"used_percent",out var used))continue;
   var minutes=window.TryGetProperty("window_minutes",out var minutesElement)&&minutesElement.ValueKind==JsonValueKind.Number&&minutesElement.TryGetInt32(out var value)?value:0;
   DateTimeOffset? resets=null;
   if(window.TryGetProperty("resets_at",out var resetElement)&&resetElement.ValueKind==JsonValueKind.Number&&resetElement.TryGetInt64(out var epoch)&&epoch>0)resets=DateTimeOffset.FromUnixTimeSeconds(epoch);
   windows.Add(new(minutes,used,resets));
  }
  if(windows.Count>0)quota=new(time,windows);
 }
 private static bool Number(JsonElement parent,string name,out double value)
 {
  value=0;
  if(!parent.TryGetProperty(name,out var element))return false;
  if(element.ValueKind==JsonValueKind.Number)return element.TryGetDouble(out value);
  return element.ValueKind==JsonValueKind.String&&double.TryParse(element.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out value);
 }
}
