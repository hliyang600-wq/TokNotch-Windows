using System.Text.Json;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Usage;
internal static class RingTests
{
 /// <summary>Codex quota records were verified against real logs: payload.rate_limits with window_minutes 300 / 10080 and unix-seconds resets_at.</summary>
 public static async Task Run(string root)
 {
  int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
  var now=DateTimeOffset.Now;
  string Codex(DateTimeOffset time,double fiveUsed,double weekUsed,long? fiveReset,long? weekReset,bool swapped=false)
  {
   var five=new{used_percent=fiveUsed,window_minutes=300,resets_at=fiveReset};
   var week=new{used_percent=weekUsed,window_minutes=10080,resets_at=weekReset};
   return JsonSerializer.Serialize(new
   {
    timestamp=time.ToString("O"),type="event_msg",
    payload=new{type="token_count",info=new{total_token_usage=new{input_tokens=100,output_tokens=10,cached_input_tokens=0,cache_write_input_tokens=0,reasoning_output_tokens=0}},rate_limits=new{primary=swapped?week:five,secondary=swapped?five:week}}
   });
  }
  /// <summary>Each case starts from an empty home so a leftover file from an earlier run can never win the "newest record" comparison.</summary>
  string Home(string name){var home=Path.Combine(root,"rings",name);if(Directory.Exists(home))Directory.Delete(home,true);Directory.CreateDirectory(Path.Combine(home,".codex","sessions"));return home;}
  ProviderUsageSnapshot Read(string home)=>new LocalUsageSource(home).Read().Vendors.First(v=>v.Provider==Provider.OpenAI);

  var fiveReset=now.AddHours(2).ToUnixTimeSeconds();var weekReset=now.AddDays(3).ToUnixTimeSeconds();
  var primary=Home("primary");File.WriteAllText(Path.Combine(primary,".codex","sessions","a.jsonl"),
   Codex(now.AddMinutes(-30),20,13,fiveReset,weekReset)+"\n"+Codex(now.AddMinutes(-4),80,13,fiveReset,weekReset)+"\n");
  var codex=Read(primary);
  Check(codex.Ring.WindowLabel=="5h"&&Math.Abs(codex.Ring.Fraction!.Value-0.20)<1e-9,"Codex five-hour window converts used percent into remaining percent");
  Check(codex.InnerRing is not null&&codex.InnerRing.WindowLabel=="周"&&Math.Abs(codex.InnerRing.Fraction!.Value-0.87)<1e-9,"Codex weekly window kept separate from the five-hour window");
  Check(codex.Ring.ResetsAt!.Value.ToUnixTimeSeconds()==fiveReset&&codex.InnerRing!.ResetsAt!.Value.ToUnixTimeSeconds()==weekReset,"reset moments retained per window");
  Check(codex.Ring.RecordedAt is {} recorded&&Math.Abs((recorded-now.AddMinutes(-4)).TotalSeconds)<2,"latest record wins inside one log");
  Check(!codex.Ring.ResetElapsed&&!codex.InnerRing!.ResetElapsed,"future reset is not stale");

  // Field order is not trusted: primary may carry the weekly window.
  File.WriteAllText(Path.Combine(primary,".codex","sessions","b.jsonl"),Codex(now.AddMinutes(-1),90,10,fiveReset,weekReset,swapped:true)+"\n");
  codex=Read(primary);
  Check(Math.Abs(codex.Ring.Fraction!.Value-0.10)<1e-9&&Math.Abs(codex.InnerRing!.Fraction!.Value-0.90)<1e-9,"windows matched by duration even when primary holds the weekly window");
  Check(codex.Ring.RecordedAt!.Value>now.AddMinutes(-2),"newest log across files wins");

  var expired=Home("expired");File.WriteAllText(Path.Combine(expired,".codex","sessions","a.jsonl"),
   Codex(now.AddMinutes(-400),50,90,now.AddMinutes(-10).ToUnixTimeSeconds(),weekReset)+"\n");
  codex=Read(expired);
  Check(Math.Abs(codex.Ring.Fraction!.Value-0.50)<1e-9&&codex.Ring.ResetElapsed,"elapsed reset keeps the recorded value and flags it instead of filling to 100%");
  Check(codex.SourceStatus!.Contains("已过重置时间"),"expired window is also called out in the status line");

  var missing=Home("missing");File.WriteAllText(Path.Combine(missing,".codex","sessions","a.jsonl"),
   Codex(now.AddMinutes(-20),30,40,null,null)+"\n");
  codex=Read(missing);
  Check(codex.Ring.ResetsAt is null&&codex.Ring.Fraction is not null,"absent resets_at stays unknown without discarding the percentage");

  var none=Home("none");File.WriteAllText(Path.Combine(none,".codex","sessions","a.jsonl"),
   JsonSerializer.Serialize(new{timestamp=now.ToString("O"),type="event_msg",payload=new{type="token_count",info=new{total_token_usage=new{input_tokens=5,output_tokens=1}}}})+"\n");
  codex=Read(none);
  Check(codex.Ring.Fraction is null&&codex.InnerRing!.Fraction is null&&codex.Ring.RecordedAt is null,"no quota record leaves both rings unknown, never zero");

  var clamped=Home("clamped");File.WriteAllText(Path.Combine(clamped,".codex","sessions","a.jsonl"),
   Codex(now.AddMinutes(-2),150,0,fiveReset,weekReset)+"\n");
  codex=Read(clamped);
  Check(codex.Ring.Fraction==0d&&codex.InnerRing!.Fraction==1d,"used above 100 clamps to empty; zero used clamps to full");
  Check(RingMath.RemainingFraction(-20)==1d,"negative used percent clamps to a full ring");

  Check(RingMath.AmountFraction(25m,50m)==0.5d,"half of the baseline draws half a circle");
  Check(RingMath.AmountFraction(0m,50m)==0d&&RingMath.AmountFraction(-3m,50m)==0d,"zero and negative balances draw an empty ring");
  Check(RingMath.AmountFraction(50m,50m)==1d&&RingMath.AmountFraction(75m,50m)==1d,"baseline and above draw a full circle without truncating the amount");
  Check(RingMath.AmountFraction(null,50m) is null&&RingMath.AmountFraction(25m,null) is null&&RingMath.AmountFraction(25m,0m) is null,"unknown balance or missing baseline stays unknown instead of zero");
  Check(RingMath.Money(23.45m,"CNY")=="¥23.45"&&RingMath.Money(-1.25m,"USD")=="-$1.25"&&RingMath.Money(null,"CNY")=="—","center money follows the source currency and keeps negatives");
  Check(RingMath.Percent(0.07d)=="7%"&&RingMath.Percent(null)=="—","remaining percent formatting");

  var preferences=new DisplayPreferences(new[]{Provider.OpenAI,Provider.Dsh,Provider.DeepSeek},DisplayPreferences.Default.Metrics,50m,null);
  Check(preferences.BaselineFor("CNY")==50m&&preferences.BaselineFor("USD") is null,"USD ring never borrows the CNY baseline");
  bool rejected=false;try{_=new DisplayPreferences(preferences.Providers,preferences.Metrics,0m,null);}catch(ArgumentException){rejected=true;}Check(rejected,"zero CNY baseline rejected");
  rejected=false;try{_=new DisplayPreferences(preferences.Providers,preferences.Metrics,50m,-2m);}catch(ArgumentException){rejected=true;}Check(rejected,"negative USD baseline rejected");
  await Task.CompletedTask;
  Console.WriteLine($"{checks} ring and quota checks passed");
 }
}
