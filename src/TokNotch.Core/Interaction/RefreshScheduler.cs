namespace TokNotch.Core.Interaction;

/// <summary>
/// The one auto-refresh loop. The interval is re-read before every wait, so a settings change takes effect at once
/// (Reschedule cancels the pending wait) rather than after the previous, possibly long, interval.
/// </summary>
public sealed class RefreshScheduler(Func<CancellationToken,Task> work)
{
 private static readonly TimeSpan Minimum=TimeSpan.FromMilliseconds(50);
 private TimeSpan _interval=TimeSpan.FromSeconds(30);
 private CancellationTokenSource _wake=new();
 public TimeSpan Interval{get=>_interval;set=>_interval=value<Minimum?Minimum:value;}
 public int Completions{get;private set;}
 public int Failures{get;private set;}
 public DateTimeOffset? LastCompletionAt{get;private set;}
 public Task Start(CancellationToken ct)=>RunAsync(ct);
 /// <summary>Ends the current wait so the next round uses the new interval immediately.</summary>
 public void Reschedule()
 {
  var previous=_wake;_wake=new CancellationTokenSource();
  try{previous.Cancel();}catch(ObjectDisposedException){}
 }
 private async Task RunAsync(CancellationToken ct)
 {
  while(!ct.IsCancellationRequested)
  {
   var wake=_wake;
   using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,wake.Token);
   try{await Task.Delay(Interval,linked.Token);}
   catch(OperationCanceledException){if(ct.IsCancellationRequested)return;continue;}
   try{await work(ct);Completions++;LastCompletionAt=DateTimeOffset.Now;}
   catch(OperationCanceledException){return;}
   catch(Exception){Failures++;}
  }
 }
}
