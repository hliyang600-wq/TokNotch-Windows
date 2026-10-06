using System.Text.Json;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Settings;
internal static class RingChoiceTests
{
 public static void Run(string root)
 {
  int checks=0;void Check(bool okay,string label){if(!okay)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
  var defaults=DisplayPreferences.Default;
  Check(defaults.RingFor(Provider.OpenAI)==new RingChoice(RingContent.FiveHourRemaining,RingContent.WeeklyRemaining)&&defaults.RingFor(Provider.Dsh)==new RingChoice(RingContent.Balance,RingContent.None),"existing Codex double ring and amount rings remain defaults");
  var choices=defaults.Rings.ToDictionary(pair=>pair.Key,pair=>pair.Value);
  choices[Provider.OpenAI]=new(RingContent.WeeklyRemaining,RingContent.TodayTokens,75_000_000);
  choices[Provider.Dsh]=new(RingContent.TodayOfMonth,RingContent.Balance,50_000_000);
  choices[Provider.Kimi]=new(RingContent.CashBalance,RingContent.GiftBalance);
  var custom=new DisplayPreferences(defaults.Providers,defaults.Metrics,70m,5m,rings:choices);
  Check(custom.RingFor(Provider.OpenAI).Inner==RingContent.TodayTokens&&custom.RingFor(Provider.Dsh).TokenBaseline==50_000_000&&custom.RingFor(Provider.Kimi).Outer==RingContent.CashBalance,"each provider keeps independent outer, inner and token scale");
  var folder=Path.Combine(root,"ring-choices");var store=new DisplayPreferencesStore(folder);store.Save(custom);var restored=new DisplayPreferencesStore(folder).Load();
  Check(restored.RingFor(Provider.OpenAI)==custom.RingFor(Provider.OpenAI)&&restored.RingFor(Provider.Dsh)==custom.RingFor(Provider.Dsh)&&restored.RingFor(Provider.Kimi)==custom.RingFor(Provider.Kimi),"custom ring content survives restart");
  Check(restored.AmountBaselineCny==70m&&restored.AmountBaselineUsd==5m,"new ring choices retain existing currency baselines");
  var oldFile=Path.Combine(folder,"config","display.json");using(var json=JsonDocument.Parse(File.ReadAllText(oldFile))){var old=json.RootElement.EnumerateObject().Where(p=>p.Name!="Rings").ToDictionary(p=>p.Name,p=>p.Value);File.WriteAllText(oldFile,JsonSerializer.Serialize(old));}
  var legacy=new DisplayPreferencesStore(folder).Load();Check(legacy.RingFor(Provider.OpenAI)==RingChoices.Default(Provider.OpenAI)&&legacy.AmountBaselineCny==70m,"older settings without ring choices load safely with existing values");
  bool Throws(Provider source,RingChoice choice){try{RingChoices.Validate(source,choice);return false;}catch(ArgumentException){return true;}}
  Check(Throws(Provider.OpenAI,new(RingContent.CashBalance,RingContent.None)),"unsupported source metric rejected");
  Check(Throws(Provider.Dsh,new(RingContent.Balance,RingContent.Balance)),"duplicate inner and outer metric rejected");
  Check(Throws(Provider.Dsh,new(RingContent.Balance,RingContent.None,0)),"zero token full-ring value rejected");
  Check(Throws(Provider.Kimi,new(RingContent.TodayTokens,RingContent.None)),"Kimi cannot show invented historical tokens");
  Console.WriteLine($"{checks} custom ring checks passed");
 }
}
