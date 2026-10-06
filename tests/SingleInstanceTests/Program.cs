using System.Diagnostics;
using TokNotch.Infrastructure.Lifecycle;

if (args.Length > 0)
{
    var mode = args[0]; var identity = args[1]; var home = args[2];
    var logGate = new object();
    using var instance = new SingleInstanceCoordinator(request =>
    {
        lock (logGate) File.AppendAllText(Path.Combine(home, "requests.txt"), request + "\n");
    }, identity);
    if (mode == "secondary")
    {
        if (instance.IsPrimary) return 2;
        return instance.NotifyAsync(SingleInstanceCoordinator.ParseRequest(args.Skip(3))).GetAwaiter().GetResult() ? 0 : 3;
    }
    if (!instance.IsPrimary) return 4;
    File.WriteAllText(Path.Combine(home, "ready"), Environment.ProcessId.ToString());
    while (!File.Exists(Path.Combine(home, "stop"))) Thread.Sleep(20);
    return 0;
}

var root = Path.GetFullPath("artifacts/single-instance-tests/" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var testIdentity = Guid.NewGuid().ToString("N");
var processes = new List<Process>();
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine("PASS " + description); checks++;
}
Process Start(string mode, params string[] extra)
{
    var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
    if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        info.ArgumentList.Add(typeof(Program).Assembly.Location);
    foreach (var argument in new[] { mode, testIdentity, root }.Concat(extra)) info.ArgumentList.Add(argument);
    var process = Process.Start(info)!; processes.Add(process); return process;
}
void Wait(Func<bool> condition)
{
    var deadline = Stopwatch.StartNew();
    while (!condition()) { if (deadline.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException(); Thread.Sleep(20); }
}
int Requests() => File.Exists(Path.Combine(root, "requests.txt")) ? File.ReadAllLines(Path.Combine(root, "requests.txt")).Length : 0;
try
{
    var first = Start("primary"); Wait(() => File.Exists(Path.Combine(root, "ready")));
    Check(!first.HasExited, "first process owns instance");
    var second = Start("secondary"); Check(second.WaitForExit(10000) && second.ExitCode == 0, "duplicate exits after notifying owner");
    Wait(() => Requests() >= 1); Check(File.ReadAllText(Path.Combine(root, "requests.txt")).Contains("Show"), "existing process receives show request");
    foreach (var pair in new[] { ("--settings", "Settings"), ("--connections", "Connections"), ("--appearance", "Appearance"), ("--mimo-login", "MimoLogin") })
    {
        var child = Start("secondary", pair.Item1); Check(child.WaitForExit(10000) && child.ExitCode == 0, pair.Item1 + " duplicate exits");
        Wait(() => File.ReadAllText(Path.Combine(root, "requests.txt")).Contains(pair.Item2));
    }
    var burst = Enumerable.Range(0, 12).Select(_ => Start("secondary")).ToArray();
    Check(burst.All(p => p.WaitForExit(10000) && p.ExitCode == 0), "12 concurrent launches create no second owner");
    File.WriteAllText(Path.Combine(root, "stop"), ""); Check(first.WaitForExit(10000) && first.ExitCode == 0, "normal exit releases ownership");
    File.Delete(Path.Combine(root, "stop")); File.Delete(Path.Combine(root, "ready"));
    var restarted = Start("primary"); Wait(() => File.Exists(Path.Combine(root, "ready")));
    Check(!restarted.HasExited, "application can restart after normal exit");
    restarted.Kill(); restarted.WaitForExit(); File.Delete(Path.Combine(root, "ready"));
    var recovered = Start("primary"); Wait(() => File.Exists(Path.Combine(root, "ready")));
    Check(!recovered.HasExited, "crashed owner cannot leave a stale application lock");
    File.WriteAllText(Path.Combine(root, "stop"), ""); recovered.WaitForExit(10000);
    Console.WriteLine($"{checks} single-instance process checks passed");
    return 0;
}
finally
{
    foreach (var process in processes) { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } process.Dispose(); }
}
