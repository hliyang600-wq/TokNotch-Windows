namespace TokNotch.Core.Models;
using TokNotch.Core.Interaction;
public enum UsageMetric { Today, Month, AllTime }
public sealed record ProviderOption(Provider Id,string Name,string Description);
public static class ProviderCatalog
{
 public static IReadOnlyList<ProviderOption> Available {get;}=Array.AsReadOnly(new[]{
  new ProviderOption(Provider.OpenAI,"Codex","本地会话日志"),new(Provider.Dsh,"DSH","DeepSeek API 余额"),new(Provider.Kimi,"Kimi","API 余额"),new(Provider.Mimo,"小米 MiMo","控制台 Token 用量"),new(Provider.DeepSeek,"DeepSeek","API 余额 / 本地 Token"),new(Provider.Qwen,"千问","Token Plan 个人版 Credits 余量")});
 public static string Name(Provider id)=>Available.FirstOrDefault(p=>p.Id==id)?.Name??id.ToString();
}
public sealed class DisplayPreferences
{
 public IReadOnlyList<Provider> Providers {get;}
 public IReadOnlyList<UsageMetric> Metrics {get;}
 public IReadOnlyDictionary<Provider,RingChoice> Rings {get;}
 public IReadOnlyDictionary<Provider,IReadOnlyList<RingContent>> Rows {get;}
 public IReadOnlyList<RingContent> RowsFor(Provider provider)=>Rows.TryGetValue(provider,out var rows)?rows:DisplayRows.Default(provider,Metrics);
 public RingChoice RingFor(Provider provider)=>Rings.TryGetValue(provider,out var choice)?choice:RingChoices.Default(provider);
 /// <summary>Full-circle baseline shared by every CNY amount ring. Codex quota rings ignore it.</summary>
 public decimal AmountBaselineCny {get;}
 /// <summary>Independent full-circle baseline for USD balances; null means "not set" and USD rings stay neutral.</summary>
 public decimal? AmountBaselineUsd {get;}
 /// <summary>How often the island re-reads local logs and republishes the snapshot, in seconds.</summary>
 public int RefreshSeconds {get;}
 /// <summary>Shortest gap between two official balance queries per provider, in seconds; the tray refresh ignores it.</summary>
 public int BalanceRefreshSeconds {get;}
 /// <summary>How the island expands; moved here from the tray menu so it survives a restart.</summary>
 public ExpansionMode Expansion {get;}
 /// <summary>Live liquid glass when true, recordable frosted material when false.</summary>
 public bool GlassEnabled {get;}
 public WindowPreferences Window {get;}
 public DisplayPreferences WithWindow(WindowPreferences window)=>new(Providers,Metrics,AmountBaselineCny,AmountBaselineUsd,RefreshSeconds,BalanceRefreshSeconds,Expansion,GlassEnabled,Rings,window,Rows);
 public const int MinimumRefreshSeconds=5;
 public const int MaximumRefreshSeconds=3600;
 public const int MinimumBalanceSeconds=30;
 public const int MaximumBalanceSeconds=86400;
 public static DisplayPreferences Default=>new(new[]{Provider.OpenAI,Provider.Dsh,Provider.Kimi},new[]{UsageMetric.Today,UsageMetric.Month,UsageMetric.AllTime});
 public DisplayPreferences(IEnumerable<Provider> providers,IEnumerable<UsageMetric> metrics,decimal amountBaselineCny=50m,decimal? amountBaselineUsd=null,int refreshSeconds=30,int balanceRefreshSeconds=300,ExpansionMode expansion=ExpansionMode.Hover,bool glassEnabled=true,IReadOnlyDictionary<Provider,RingChoice>? rings=null,WindowPreferences? window=null,IReadOnlyDictionary<Provider,IReadOnlyList<RingContent>>? rows=null)
 {
  var selected=providers.ToArray();var order=metrics.ToArray();
  if(selected.Length!=3||selected.Distinct().Count()!=3||selected.Any(p=>!ProviderCatalog.Available.Any(v=>v.Id==p)))throw new ArgumentException("请选择三个不同的数据来源");
  if(order.Length!=3||order.Distinct().Count()!=3||order.Any(m=>!Enum.IsDefined(m)))throw new ArgumentException("指标必须包含今日、本月、累计且不重复");
  if(amountBaselineCny<=0m)throw new ArgumentException("人民币满圈金额必须大于零");
  if(amountBaselineUsd is decimal usd&&usd<=0m)throw new ArgumentException("美元满圈金额必须大于零或留空");
  if(refreshSeconds<MinimumRefreshSeconds||refreshSeconds>MaximumRefreshSeconds)throw new ArgumentException($"界面刷新间隔需在 {MinimumRefreshSeconds}–{MaximumRefreshSeconds} 秒之间");
  if(balanceRefreshSeconds<MinimumBalanceSeconds||balanceRefreshSeconds>MaximumBalanceSeconds)throw new ArgumentException($"余额查询间隔需在 {MinimumBalanceSeconds}–{MaximumBalanceSeconds} 秒之间");
  if(!Enum.IsDefined(expansion))throw new ArgumentException("展开方式无效");
  Window=window??new();Window.Validate();
  var choices=new Dictionary<Provider,RingChoice>();foreach(var option in ProviderCatalog.Available){var choice=rings!=null&&rings.TryGetValue(option.Id,out var custom)?custom:RingChoices.Default(option.Id);RingChoices.Validate(option.Id,choice);choices.Add(option.Id,choice);}
  if(rings!=null&&rings.Keys.Any(id=>!choices.ContainsKey(id)))throw new ArgumentException("圆环来源无效。");
  var content=new Dictionary<Provider,IReadOnlyList<RingContent>>();
  foreach(var option in ProviderCatalog.Available){var selectedRows=rows!=null&&rows.TryGetValue(option.Id,out var customRows)?customRows:DisplayRows.Default(option.Id,order);DisplayRows.Validate(option.Id,selectedRows);content.Add(option.Id,Array.AsReadOnly(selectedRows.ToArray()));}
  if(rows!=null&&rows.Keys.Any(id=>!content.ContainsKey(id)))throw new ArgumentException("显示指标来源无效。");
  Rows=new System.Collections.ObjectModel.ReadOnlyDictionary<Provider,IReadOnlyList<RingContent>>(content);
  Providers=Array.AsReadOnly(selected);Metrics=Array.AsReadOnly(order);Rings=new System.Collections.ObjectModel.ReadOnlyDictionary<Provider,RingChoice>(choices);AmountBaselineCny=amountBaselineCny;AmountBaselineUsd=amountBaselineUsd;RefreshSeconds=refreshSeconds;BalanceRefreshSeconds=balanceRefreshSeconds;Expansion=expansion;GlassEnabled=glassEnabled;
 }
 /// <summary>A USD balance never borrows the CNY baseline.</summary>
 public decimal? BaselineFor(string currency)=>currency.Equals("USD",StringComparison.OrdinalIgnoreCase)?AmountBaselineUsd:AmountBaselineCny;
}
