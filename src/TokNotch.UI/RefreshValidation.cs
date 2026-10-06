using System.Threading;
using TokNotch.Core.Interaction;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;
/// <summary>Proves the automatic refresh loop really runs, publishes newer snapshots and reacts to an interval change.</summary>
internal static class RefreshValidation
{
 internal static async Task RunAsync(IslandWindow island,IslandViewModel model,Func<CancellationToken,Task> refresh)
 {
  var output=ApplicationPaths.ArtifactsDirectory;Directory.CreateDirectory(output);
  var checks=new List<string>();
  void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
  var ring=(System.Windows.Controls.Button)island.FindName("RingRefreshButton");
  var mode=island.Policy.Mode;var target=island.Policy.TargetExpanded;
  var beforeClick=model.Snapshot?.GeneratedAt??DateTimeOffset.MinValue;
  var click=new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent);
  ring.RaiseEvent(click);
  Check(click.Handled&&!ring.IsEnabled,"ring click is handled and disables repeated clicks during refresh");
  for(int i=0;i<200&&!ring.IsEnabled;i++)await Task.Delay(20);
  Check(ring.IsEnabled&&model.Snapshot?.GeneratedAt>beforeClick,"ring click invokes the existing immediate refresh and restores the button");
  Check(island.Policy.Mode==mode&&island.Policy.TargetExpanded==target,"ring refresh does not toggle expansion or change the interaction mode");
  var scheduler=new RefreshScheduler(refresh){Interval=TimeSpan.FromMinutes(30)};
  using var stop=new CancellationTokenSource();
  var loop=scheduler.Start(stop.Token);
  await Task.Delay(300);
  Check(scheduler.Completions==0&&scheduler.Failures==0,"a long interval does not refresh early");
  var first=model.Snapshot?.GeneratedAt??DateTimeOffset.MinValue;
  scheduler.Interval=TimeSpan.FromMilliseconds(120);scheduler.Reschedule();
  await Task.Delay(900);
  Check(scheduler.Completions>=3,"the automatic refresh loop keeps firing at the configured interval");
  Check(model.Snapshot is not null&&model.Snapshot.GeneratedAt>first,"every automatic refresh publishes a newer snapshot to the island");
  Check(model.RefreshStamp.Length>0&&model.RingTooltip.Contains(model.RefreshStamp),"the ring hover shows the time of the last refresh");
  var beforeReschedule=scheduler.Completions;
  scheduler.Reschedule();
  await Task.Delay(260);
  Check(scheduler.Completions>beforeReschedule,"an interval change reschedules at once instead of waiting out the old interval");
  stop.Cancel();
  try{await loop;}catch(OperationCanceledException){}
  var settled=scheduler.Completions;await Task.Delay(300);
  Check(scheduler.Completions==settled&&model.RefreshStamp.Length>0,"cancelling stops the loop and keeps the last refresh stamp");
  var failing=new RefreshScheduler(_=>throw new InvalidOperationException("fixture")){Interval=TimeSpan.FromMilliseconds(80)};
  using var stopFailing=new CancellationTokenSource();
  var failingLoop=failing.Start(stopFailing.Token);
  await Task.Delay(400);stopFailing.Cancel();
  try{await failingLoop;}catch(OperationCanceledException){}
  Check(failing.Failures>=3&&failing.Completions==0,"a throwing refresh is counted as a failure and never kills the loop");
  await File.WriteAllLinesAsync(Path.Combine(output,"refresh-checks.txt"),checks);
 }
}
