using System.ComponentModel;
using System.Globalization;
using TokNotch.Core.Models;
namespace TokNotch.UI.ViewModels;

public sealed class IslandViewModel : INotifyPropertyChanged
{
    private UsageSnapshot? _snapshot;
    private Provider _selected = Provider.Claude;
    public DisplayPreferences Preferences {get;private set;}=DisplayPreferences.Default;
    public void Configure(DisplayPreferences preferences){Preferences=preferences;if(!preferences.Providers.Contains(_selected))_selected=preferences.Providers[0];Notify();}
    public string FirstProviderLabel=>ProviderCatalog.Name(Preferences.Providers[0]);
    public string SecondProviderLabel=>ProviderCatalog.Name(Preferences.Providers[1]);
    public string ThirdProviderLabel=>ProviderCatalog.Name(Preferences.Providers[2]);
    private string SelectionColor=>TokNotch.UI.Themes.ThemeManager.IsLight?"#16000000":"#20FFFFFF";
    public string FirstProviderBackground=>Selected==FirstProvider?SelectionColor:"Transparent";
    public string SecondProviderBackground=>Selected==SecondProvider?SelectionColor:"Transparent";
    public string ThirdProviderBackground=>Selected==ThirdProvider?SelectionColor:"Transparent";
    internal void RefreshTheme()=>Notify();
    public Provider FirstProvider=>Preferences.Providers[0];
    public Provider SecondProvider=>Preferences.Providers[1];
    public Provider ThirdProvider=>Preferences.Providers[2];
    private bool BalanceRows=>Live&&Preferences.RowsFor(Selected).Any(m=>m is RingContent.Balance or RingContent.CashBalance or RingContent.GiftBalance);
    private RingContent RowMetric(int index)=>Live?Preferences.RowsFor(Selected)[index]:DisplayRows.FromLegacy(Preferences.Metrics[index]);
    public bool FirstMetricVisible=>RowMetric(0)!=RingContent.None;
    public bool SecondMetricVisible=>RowMetric(1)!=RingContent.None;
    public bool ThirdMetricVisible=>RowMetric(2)!=RingContent.None;
    public bool HasMetricRows=>FirstMetricVisible||SecondMetricVisible||ThirdMetricVisible;
    public string FirstMetricHeight=>FirstMetricVisible?"*":"0";
    public string SecondMetricHeight=>SecondMetricVisible?"*":"0";
    public string ThirdMetricHeight=>ThirdMetricVisible?"*":"0";
    public string FirstMetricLabel=>MetricLabel(RowMetric(0));
    public string SecondMetricLabel=>MetricLabel(RowMetric(1));
    public string ThirdMetricLabel=>MetricLabel(RowMetric(2));
    public double? FirstMetricValue=>MetricValue(RowMetric(0));
    public double? SecondMetricValue=>MetricValue(RowMetric(1));
    public double? ThirdMetricValue=>MetricValue(RowMetric(2));
    public string FirstMetricFormat=>RowFormat(RowMetric(0));
    public string SecondMetricFormat=>RowFormat(RowMetric(1));
    public string ThirdMetricFormat=>RowFormat(RowMetric(2));
    private string RowFormat(RingContent metric)=>metric switch
    {
        RingContent.Balance or RingContent.CashBalance or RingContent.GiftBalance=>Current?.Currency=="USD"?"Currency":"Cny",
        RingContent.FiveHourRemaining or RingContent.WeeklyRemaining or RingContent.TodayOfMonth or RingContent.MonthlyRemaining or RingContent.MonthlyUsed=>"Percent",
        RingContent.QuotaResetTime=>"ResetTime",
        RingContent.AllTimeTokens=>"Compact",
        _=>Live?"Compact":"Currency"
    };
    private string MetricLabel(RingContent metric)=>metric switch
    {
        RingContent.None=>"",
        RingContent.TodayTokens=>Selected==Provider.Mimo?"今日 UTC":Selected==Provider.Qwen?"今日 Token":"今日",
        RingContent.MonthTokens=>Selected==Provider.Mimo?"本月 UTC":"本月",
        RingContent.TodayOfMonth when Selected==Provider.Mimo=>"今日占本月 UTC",
        RingContent.AllTimeTokens=>Live?"累计":"TOKENS",
        RingContent.Balance when Selected==Provider.Dsh&&Current?.BalanceSource is {} source=>source+" 余额",
        _=>RingChoices.Name(metric)
    };
    private double? MetricValue(RingContent metric)=>metric switch
    {
        RingContent.TodayTokens=>Live?Available(Current?.Today):KnownCost(Current?.Today),
        RingContent.MonthTokens=>Live?Available(Current?.Month):KnownCost(Current?.Month),
        RingContent.AllTimeTokens=>Live?Available(Current?.AllTime):Current is null||Current.Detection==DetectionState.NotDetected?null:Current.Month.Tokens.Total,
        RingContent.Balance=>(double?)Current?.Balance,
        RingContent.CashBalance=>(double?)Current?.Cash,
        RingContent.GiftBalance=>(double?)Current?.Voucher,
        RingContent.QuotaResetTime=>Current?.Ring.ResetsAt?.ToUnixTimeSeconds(),
        _=>RingValue(metric)
    };
    public event PropertyChangedEventHandler? PropertyChanged;
    public UsageSnapshot? Snapshot => _snapshot;
    public Provider Selected => _selected;
    public ProviderUsageSnapshot? Current => _snapshot?.Vendors.FirstOrDefault(p => p.Provider == _selected);
    public bool Live => _snapshot?.IsDemo == false;
    public string DetailBadge => Live ? "用量概览" : "DEMO · MOCK DATA";
    public string Title => Current?.Model ?? "AI usage";
    /// <summary>Visible proof that the automatic refresh is alive: the moment the current snapshot was produced.</summary>
    public string RefreshStamp => Live && _snapshot is { } snapshot ? $"{snapshot.GeneratedAt.LocalDateTime:HH:mm:ss} 刷新" : "";
    /// <summary>The former bottom status line; it now lives in the ring hover so the island stays clean.</summary>
    private string StatusLine
    {
        get
        {
            var text = (BalanceRows ? Current?.BalanceStatus ?? Current?.SourceStatus : Current?.SourceStatus) ?? "读取中…";
            if (_snapshot?.RefreshState == RefreshState.Refreshing) text = "刷新中…";
            else if (CurrentRefreshFailed && Current?.RefreshFailed != true) text = _snapshot?.StatusMessage ?? "刷新失败";
            return RefreshStamp.Length == 0 ? text : $"{text} · {RefreshStamp}";
        }
    }
    private string CompactValue()=>Selected==Provider.Qwen?RingMath.Percent(RingValue(RingContent.MonthlyRemaining)):Selected is Provider.Kimi or Provider.DeepSeek ? Current?.Balance is decimal b?$"{(Current.Currency=="USD"?"$":"¥")}{b:0.00}":"—" : LocalCount(Selected);
    private string LocalCount(Provider provider){ var vendor=_snapshot?.Vendors.FirstOrDefault(v=>v.Provider==provider); return vendor?.Detection is DetectionState.Ready or DetectionState.DetectedNoUsage ? FormatTokens(vendor.Today.Tokens.Total) : "—"; }
    public string CollapsedText => Live ? $"{ProviderCatalog.Name(_selected)} {CompactValue()}" : _snapshot?.RefreshState == RefreshState.Failed ? "Refresh failed" : Current?.Detection switch
    {
        DetectionState.NotDetected => "Not detected",
        DetectionState.DetectedNoUsage => "No usage yet",
        _ when Current?.Month.CostStatus == CostStatus.Unavailable => "Price unavailable",
        _ => $"Claude {Ratio(Provider.Claude)}  ·  Codex {Ratio(Provider.OpenAI)}"
    };
    private RingChoice RingChoice => Preferences.RingFor(Selected);
    /// <summary>Each visible source picks its own outer and optional inner measure.</summary>
    public double? RingFraction => Live ? RingValue(RingChoice.Outer) : Current?.Ring.Fraction;
    public double? InnerRingFraction => ShowInnerRing ? RingValue(RingChoice.Inner) : null;
    public bool ShowInnerRing => Live && RingChoice.Inner != RingContent.None;
    public string RingIdentity => $"{Current?.Ring.StableId ?? "unknown"}:{RingChoice.Outer}:{RingChoice.Inner}";
    public string RingInnerAccent => TokNotch.UI.Themes.ThemeManager.IsLight?(Selected==Provider.OpenAI?"#9C6A12":"#7557A5"):(Selected == Provider.OpenAI ? "#E6B966" : "#C4B0E7");
    public double RingCenterFontSize => RingCenterPrimary.Length switch { > 10 => 10d, > 8 => 11d, > 6 => 12d, _ when ShowInnerRing => 12d, _ => 16d };
    public string RingCenterPrimary => Live ? LiveCenterPrimary : DemoCenter;
    public string RingCenterSecondary => Live && ShowInnerRing ? RingText(RingChoice.Inner) : "";
    private bool CurrentRefreshFailed => Current?.RefreshFailed == true || (_snapshot?.RefreshState == RefreshState.Failed && !_snapshot.Vendors.Any(v=>v.RefreshFailed));
    public string CenterLabel => _snapshot?.RefreshState switch { RefreshState.Refreshing => "刷新中…", _ when CurrentRefreshFailed => "部分未更新", _ => Live ? LiveCenterCaption : "VALUE / PLAN" };
    public string RingTooltip => Live ? LiveRingTooltip : Current?.Ring.BaselineLabel ?? "DEMO";
    private long? RingTokens(RingContent content)
    {
        var current=Current;
        if(current?.TokenUsageAvailable!=true||current.Detection is not (DetectionState.Ready or DetectionState.DetectedNoUsage))return null;
        return content switch { RingContent.TodayTokens=>current.Today.Tokens.Total,RingContent.MonthTokens=>current.Month.Tokens.Total,RingContent.AllTimeTokens=>current.AllTime.Tokens.Total,_=>null };
    }
    private double? RingValue(RingContent content)
    {
        var current=Current;if(current==null)return null;
        return content switch
        {
            RingContent.FiveHourRemaining=>current.Ring.Fraction,
            RingContent.WeeklyRemaining=>current.InnerRing?.Fraction,
            RingContent.MonthlyRemaining=>current.Ring.Fraction,
            RingContent.MonthlyUsed=>current.Ring.Fraction is double remaining?1-remaining:null,
            RingContent.Balance=>RingMath.AmountFraction(current.Balance,Preferences.BaselineFor(current.Currency)),
            RingContent.CashBalance=>RingMath.AmountFraction(current.Cash,Preferences.BaselineFor(current.Currency)),
            RingContent.GiftBalance=>RingMath.AmountFraction(current.Voucher,Preferences.BaselineFor(current.Currency)),
            RingContent.TodayTokens or RingContent.MonthTokens or RingContent.AllTimeTokens=>RingTokens(content) is long tokens?Math.Clamp((double)tokens/RingChoice.TokenBaseline,0,1):null,
            RingContent.TodayOfMonth=>current.TokenUsageAvailable&&(current.Detection is DetectionState.Ready or DetectionState.DetectedNoUsage)&&current.Month.Tokens.Total>0?Math.Clamp((double)current.Today.Tokens.Total/current.Month.Tokens.Total,0,1):null,
            _=>null
        };
    }
    private string RingText(RingContent content)
    {
        var current=Current;if(current==null)return "—";
        return content switch
        {
            RingContent.FiveHourRemaining=>$"5h {RingMath.Percent(RingValue(content))}",
            RingContent.WeeklyRemaining=>$"周 {RingMath.Percent(RingValue(content))}",
            RingContent.Balance=>RingMath.Money(current.Balance,current.Currency),
            RingContent.CashBalance=>RingMath.Money(current.Cash,current.Currency),
            RingContent.GiftBalance=>RingMath.Money(current.Voucher,current.Currency),
            RingContent.TodayTokens or RingContent.MonthTokens or RingContent.AllTimeTokens=>RingTokens(content) is long value?FormatTokens(value):"—",
            RingContent.TodayOfMonth=>RingMath.Percent(RingValue(content)),
            RingContent.MonthlyRemaining or RingContent.MonthlyUsed=>RingMath.Percent(RingValue(content)),
            _=>"—"
        };
    }
    private string LiveCenterPrimary => RingText(RingChoice.Outer);
    private string LiveCenterCaption
    {
        get
        {
            if (Current is null) return "读取中";
            var content=RingChoice.Outer;
            if(Selected==Provider.Mimo&&content is RingContent.TodayTokens or RingContent.MonthTokens or RingContent.TodayOfMonth)return MetricLabel(content);
            if(content is not (RingContent.FiveHourRemaining or RingContent.WeeklyRemaining))return RingChoices.Name(content);
            var quota=content==RingContent.FiveHourRemaining?Current.Ring:Current.InnerRing;
            if(quota?.RecordedAt is not DateTimeOffset recorded)return "无额度记录";
            if(quota.ResetElapsed)return "上次记录";
            return DateTimeOffset.Now-recorded<=TimeSpan.FromMinutes(5)?"额度快照":"上次记录";
        }
    }
    private string LiveRingTooltip
    {
        get
        {
            var current = Current; if (current is null) return "读取中…";
            var choice=RingChoice;
            if (current.Provider == Provider.OpenAI&&choice==RingChoices.Default(Provider.OpenAI))
            {
                var quotaLines = new List<string>(3)
                {
                    CodexWindowText("五小时", current.Ring),
                    CodexWindowText("一周", current.InnerRing)
                };
                quotaLines.Add("本地 Codex 会话日志快照 · 不是实时额度 · 不换算成人民币");
                quotaLines.Add(StatusLine);
                return string.Join(Environment.NewLine, quotaLines);
            }
            string Detail(RingContent content)
            {
                if(content==RingContent.FiveHourRemaining)return CodexWindowText("五小时",current.Ring);
                if(content==RingContent.WeeklyRemaining)return CodexWindowText("一周",current.InnerRing);
                var label=Selected==Provider.Mimo&&content is RingContent.TodayTokens or RingContent.MonthTokens or RingContent.TodayOfMonth?MetricLabel(content):RingChoices.Name(content);
                var line=$"{label}：{RingText(content)}";
                if(content is RingContent.TodayTokens or RingContent.MonthTokens or RingContent.AllTimeTokens)line+=$" · 满圈 {FormatTokens(choice.TokenBaseline)} Token";
                if(content is RingContent.Balance or RingContent.CashBalance or RingContent.GiftBalance)line+=current.Currency=="USD"?Preferences.AmountBaselineUsd is decimal usd?$" · 满圈 ${usd:0.##}":" · 美元满圈未设置":$" · 满圈 ¥{Preferences.AmountBaselineCny:0.##}";
                if(content==RingContent.TodayOfMonth)line+=" · 本月为零时显示 —";
                if(content is RingContent.MonthlyRemaining or RingContent.MonthlyUsed)line+=$" · Credits 月额度 · 重置 {Local(current.Ring.ResetsAt)}";
                return line;
            }
            var lines=new List<string>{"外圈 " + Detail(choice.Outer)};
            if(choice.Inner!=RingContent.None)lines.Add("内圈 " + Detail(choice.Inner));
            lines.Add(StatusLine);return string.Join(Environment.NewLine,lines);
        }
    }
    private static string CodexWindowText(string name,RingSnapshot? ring)
    {
        if (ring is null || ring.RecordedAt is null) return $"{name}窗口：无记录";
        var text = $"{name}窗口：剩余 {RingMath.Percent(ring.Fraction)} · 重置 {Local(ring.ResetsAt)} · 记录 {Local(ring.RecordedAt)}";
        return ring.ResetElapsed ? text + " · 已过重置时间，数字为上次记录" : text;
    }
    private static string Local(DateTimeOffset? moment) => moment is DateTimeOffset value ? value.LocalDateTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) : "未知";
    private string DemoCenter => PaybackValue is double value ? value.ToString("0.0'x'", CultureInfo.InvariantCulture) : "—";
    public string Accent => TokNotch.UI.Themes.ThemeManager.IsLight?Selected switch {Provider.OpenAI=>"#337D66",Provider.Dsh=>"#397DA8",Provider.Mimo=>"#B4682F",Provider.Kimi or Provider.Gemini=>"#7958A3",_=>"#85664E"}:Selected switch { Provider.Claude => "#DB9B7E", Provider.OpenAI => "#8EBEAE", Provider.Gemini => "#B4A6D4", Provider.Dsh => "#8ABFE0", Provider.Kimi => "#B4A6D4", Provider.Mimo => "#F2AD7D", _ => "#AAA8AF" };
    public double? TodayValue => Live ? Selected==Provider.Kimi ? (double?)Current?.Cash : Available(Current?.Today) : KnownCost(Current?.Today);
    public double? PaybackValue => Live ? Selected==Provider.Kimi ? (double?)Current?.Balance : Available(Current?.Today) : Current?.Month.CostStatus == CostStatus.Complete && Current.Subscription?.PaybackRatio is decimal ratio ? (double)ratio : null;
    private double? Available(PeriodUsage? usage) => Current?.TokenUsageAvailable==true&&Current.Detection is DetectionState.Ready or DetectionState.DetectedNoUsage ? usage?.Tokens.Total : null;
    private static double? KnownCost(PeriodUsage? usage) => usage?.CostStatus == CostStatus.Complete && usage.EstimatedCost is decimal cost ? (double)cost : null;
    public void Apply(UsageSnapshot snapshot) { _snapshot = snapshot; Notify(); }
    public void Select(Provider provider) { _selected = provider; Notify(); }
    private string Ratio(Provider provider) => _snapshot?.Vendors.FirstOrDefault(p => p.Provider == provider)?.Subscription?.PaybackRatio is decimal r ? $"{r:0.0}x" : "—";
    public static string FormatTokens(long value) => value >= 1_000_000_000 ? $"{value / 1_000_000_000d:0.0}B" : value >= 1_000_000 ? $"{value / 1_000_000d:0.0}M" : value >= 1_000 ? $"{value / 1_000d:0.0}K" : value.ToString(CultureInfo.InvariantCulture);
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
