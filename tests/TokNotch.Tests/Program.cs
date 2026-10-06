using TokNotch.Core.Abstractions;
using TokNotch.Core.Interaction;
using TokNotch.Core.Models;
using TokNotch.Infrastructure.Usage;
using TokNotch.Core.Animation;
var passed = 0;
void Check(bool condition,string name) { if(!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
Check(new TokenCounts(1,2,3,4,5).Total==15,"five disjoint token buckets");
try { _=new TokenCounts(-1,0,0,0,0); throw new Exception("negative accepted"); } catch(ArgumentOutOfRangeException) { passed++; }
try { _=new TokenCounts(long.MaxValue,1,0,0,0); throw new Exception("overflow accepted"); } catch(OverflowException) { passed++; }
Check(new SubscriptionUsage(0,10,new(2026,1,1),new(2026,2,1)).PaybackRatio is null,"zero plan price unknown ratio");
var mock=new MockUsageSource(); var normal=await mock.GetUsageAsync(); Check(normal.IsDemo && normal.Vendors.Count==3,"mock snapshot");
mock.Scenario=DemoScenario.PriceUnavailable; var unknown=await mock.GetUsageAsync(); Check(unknown.MonthCost is null && unknown.MonthTokens>0,"unknown price keeps tokens and null cost");
mock.Scenario=DemoScenario.NoUsage; var empty=await mock.GetUsageAsync(); Check(empty.MonthTokens==0,"no usage zero tokens");
mock.Scenario=DemoScenario.RefreshFailed; var failed=await mock.GetUsageAsync(); Check(failed.RefreshState==RefreshState.Failed && failed.MonthTokens==normal.MonthTokens,"refresh failure retains snapshot");
var scheduler=new ManualScheduler(); using var state=new IslandStateController(scheduler);
state.PointerEnter(); Check(state.TargetExpanded,"hover enter"); state.AnimationSettled(true); state.PointerLeave(); Check(state.State==IslandState.CollapsePending,"delayed leave"); state.PointerEnter(); scheduler.Fire(); Check(state.TargetExpanded,"reenter cancels collapse");
state.PointerLeave(); scheduler.Fire(); Check(!state.TargetExpanded,"leave collapses after delay");
state.SetMode(ExpansionMode.Click); state.PointerEnter(); Check(!state.TargetExpanded,"click mode ignores hover");
for(var i=0;i<100;i++) { state.Click(); state.AnimationSettled(state.TargetExpanded); }
Check(!state.TargetExpanded,"100 click toggles"); state.SetMode(ExpansionMode.AlwaysExpanded); state.PointerLeave(); scheduler.Fire(); Check(state.TargetExpanded,"always expanded ignores leave");
state.SetMode(ExpansionMode.Hover); state.PointerEnter(); state.PointerLeave(); state.Dispose(); scheduler.Fire(); Check(state.TargetExpanded,"disposed pending timer cannot mutate");
var spring = new ScalarSpring(); spring.Retarget(1); spring.Advance(.08);
var position = spring.Value; var velocity = spring.Velocity; spring.Retarget(0, 32);
Check(spring.Value == position && spring.Velocity == velocity,"spring reversal preserves position and velocity");
spring.Advance(.001); Check(Math.Abs(spring.Value-position)<.02,"spring reversal no position jump");
spring.Advance(2); Check(spring.IsSettled && Math.Abs(spring.Value)<.0005,"spring eventually settles");
var sixty=new ScalarSpring(); var oneTwenty=new ScalarSpring(); sixty.Retarget(1); oneTwenty.Retarget(1);
for(var i=0;i<30;i++) sixty.Advance(1d/60); for(var i=0;i<60;i++) oneTwenty.Advance(1d/120);
Check(Math.Abs(sixty.Value-oneTwenty.Value)<1e-12 && Math.Abs(sixty.Velocity-oneTwenty.Velocity)<1e-12,"spring frame-rate independence");
var stressed=new ScalarSpring(); for(var i=0;i<1000;i++) { stressed.Retarget(i%2,24); stressed.Advance(.007); }
Check(double.IsFinite(stressed.Value) && double.IsFinite(stressed.Velocity),"1000 spring reversals stay finite");
stressed.Retarget(1); stressed.Advance(60); Check(stressed.IsSettled,"long frame stable analytic solution");
stressed.Snap(0); Check(stressed.Value==0 && stressed.Velocity==0 && stressed.Target==0,"reduced motion snap clears velocity");
try { stressed.Advance(double.NaN); throw new Exception("NaN accepted"); } catch(ArgumentOutOfRangeException) { passed++; }
var elastic=new ScalarSpring();elastic.Retarget(1,28,.68);double peak=0;
for(int i=0;i<60;i++){elastic.Advance(1d/120);peak=Math.Max(peak,elastic.Value);}
Check(peak>1.04&&peak<1.07,"normal elastic spring visibly overshoots and stays restrained");
Check(Math.Abs(elastic.Value-1)<.001,"elastic expansion settles after the recoil");
var elastic60=new ScalarSpring();var elastic144=new ScalarSpring();elastic60.Retarget(1,28,.68);elastic144.Retarget(1,28,.68);
for(int i=0;i<30;i++)elastic60.Advance(1d/60);for(int i=0;i<72;i++)elastic144.Advance(1d/144);
Check(Math.Abs(elastic60.Value-elastic144.Value)<1e-12&&Math.Abs(elastic60.Velocity-elastic144.Velocity)<1e-12,"elastic animation is independent of display frame rate");
var reverse=new ScalarSpring();reverse.Retarget(1,28,.68);reverse.Advance(.12);var reversePosition=reverse.Value;var reverseVelocity=reverse.Velocity;reverse.Retarget(0,32,.76);
Check(reverse.Value==reversePosition&&reverse.Velocity==reverseVelocity,"elastic reversal keeps both position and velocity");
reverse.Snap(1);reverse.Retarget(0,32,.76);double minimum=1;for(int i=0;i<120;i++){reverse.Advance(1d/120);minimum=Math.Min(minimum,reverse.Value);}
Check(minimum<-.015&&minimum>-.04,"collapse has a smaller bounded recoil");
var jitter=new ScalarSpring();for(int i=0;i<1000;i++){jitter.Retarget(i%2,28,.68);jitter.Advance(.007);}jitter.Advance(5);
Check(jitter.IsSettled&&double.IsFinite(jitter.Value)&&double.IsFinite(jitter.Velocity),"rapid elastic retargeting remains finite and eventually rests");
Check(380+200*IslandGeometry.MaximumSpringProgress-200<=IslandGeometry.HostWidth&&220+188*IslandGeometry.MaximumSpringProgress-188<=IslandGeometry.HostHeight,"fixed transparent host contains the maximum elastic outline");
try{elastic.Retarget(1,28,0);throw new Exception("zero damping accepted");}catch(ArgumentOutOfRangeException){passed++;}
Console.WriteLine($"{passed} checks passed");
sealed class ManualScheduler : IDeferredScheduler { private Action? _action; public IDisposable Schedule(TimeSpan delay,Action action) { _action=action; return new Cancel(()=>_action=null); } public void Fire() { var action=_action; _action=null; action?.Invoke(); } private sealed class Cancel(Action action):IDisposable { public void Dispose()=>action(); } }
