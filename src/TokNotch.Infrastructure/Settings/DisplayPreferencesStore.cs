using System.Text.Json;
using System.Text.Json.Serialization;
using TokNotch.Core.Interaction;
using TokNotch.Core.Models;
namespace TokNotch.Infrastructure.Settings;
public sealed class DisplayPreferencesStore(string projectRoot)
{
 private readonly string file=Path.Combine(projectRoot,"config","display.json");
 private static readonly JsonSerializerOptions options=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
 private sealed record Document(Provider[]? Providers,UsageMetric[]? Metrics,decimal? AmountBaselineCny,decimal? AmountBaselineUsd,int? RefreshSeconds,int? BalanceRefreshSeconds,ExpansionMode? Expansion,bool? GlassEnabled,Dictionary<Provider,RingChoice>? Rings,WindowPreferences? Window,Dictionary<Provider,RingContent[]>? Rows);
 public string? LoadWarning {get;private set;}
 public DisplayPreferences Load()
 {
  try{if(!File.Exists(file))return DisplayPreferences.Default;var doc=JsonSerializer.Deserialize<Document>(File.ReadAllText(file),options)??throw new JsonException();if(doc.Providers is null||doc.Metrics is null)throw new JsonException();return new(doc.Providers,doc.Metrics,doc.AmountBaselineCny??RingMath.DefaultAmountBaseline,doc.AmountBaselineUsd,Sanitize(doc.RefreshSeconds??30,DisplayPreferences.MinimumRefreshSeconds,DisplayPreferences.MaximumRefreshSeconds),Sanitize(doc.BalanceRefreshSeconds??300,DisplayPreferences.MinimumBalanceSeconds,DisplayPreferences.MaximumBalanceSeconds),doc.Expansion is {} mode&&Enum.IsDefined(mode)?mode:ExpansionMode.Hover,doc.GlassEnabled??true,doc.Rings,doc.Window,doc.Rows?.ToDictionary(p=>p.Key,p=>(IReadOnlyList<RingContent>)p.Value));}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException){LoadWarning="显示设置无法读取，已恢复默认顺序";return DisplayPreferences.Default;}
 }
 /// <summary>A hand-edited out-of-range interval falls back to the nearest legal value instead of discarding the other settings.</summary>
 private static int Sanitize(int value,int minimum,int maximum)=>Math.Clamp(value,minimum,maximum);
 public void Save(DisplayPreferences preferences)
 {
  Directory.CreateDirectory(Path.GetDirectoryName(file)!);var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllText(temp,JsonSerializer.Serialize(new Document(preferences.Providers.ToArray(),preferences.Metrics.ToArray(),preferences.AmountBaselineCny,preferences.AmountBaselineUsd,preferences.RefreshSeconds,preferences.BalanceRefreshSeconds,preferences.Expansion,preferences.GlassEnabled,preferences.Rings.ToDictionary(pair=>pair.Key,pair=>pair.Value),preferences.Window,preferences.Rows.ToDictionary(p=>p.Key,p=>p.Value.ToArray())),options));if(File.Exists(file)){var backup=Path.Combine(projectRoot,"backups","settings");Directory.CreateDirectory(backup);File.Copy(file,Path.Combine(backup,"display-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff")+".json"));}File.Move(temp,file,true);LoadWarning=null;}finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
