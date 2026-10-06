using TokNotch.Infrastructure.Authentication;
using TokNotch.Infrastructure.Lifecycle;
using TokNotch.UI.Authentication;
using System.Threading;
using System.Windows;
using TokNotch.Core.Models;
using TokNotch.Core.Interaction;
using TokNotch.Infrastructure.Settings;
using TokNotch.Infrastructure.Tray;
using TokNotch.Infrastructure.Usage;
using TokNotch.UI.ViewModels;
namespace TokNotch.UI;
public partial class App : Application
{
 private TrayService? tray;
 private SingleInstanceCoordinator? singleInstance;
 private readonly CancellationTokenSource stop=new();
 private readonly SemaphoreSlim refreshGate=new(1,1);
 private readonly LocalUsageSource logs=new();
 private readonly KimiBalanceSource kimi=new();
 private readonly MimoUsageSource mimo=new();
 private readonly DeepSeekApiSource deepSeek=new();
 private IslandViewModel? liveModel;
 private IslandWindow? shell;
 private RefreshScheduler? scheduler;
 private SettingsWindow? settingsWindow;
 private MimoLoginWindow? loginWindow; private Task<string>? loginTask;
 private static string ProjectRoot=>ApplicationPaths.ReportRoot;
 private readonly MimoSessionVault sessionVault=new(ApplicationPaths.DataRoot);
 private readonly ApiKeyVault keyVault=new(ApplicationPaths.DataRoot);
 private readonly DisplayPreferencesStore preferencesStore=new(ApplicationPaths.DataRoot);
 protected override async void OnStartup(StartupEventArgs e)
 {
  bool diagnostic=e.Args.Any(a=>a is "--validate" or "--live-validate" or "--glass-validate" or "--glass-position-validate" or "--auth-validate" or "--data-validate" or "--balance-validate" or "--glass-review" or "--glass-rate-validate");
  if(!diagnostic)
  {
   singleInstance=new(request=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>ActivateExisting(request)));});
   if(!singleInstance.IsPrimary){await singleInstance.NotifyAsync(SingleInstanceCoordinator.ParseRequest(e.Args));Shutdown();return;}
  }
  base.OnStartup(e);if(e.Args.Contains("--auth-validate")){try{await AuthenticationValidation.RunAsync();Shutdown();}catch(Exception error){var report=Path.Combine(ApplicationPaths.ArtifactsDirectory,"authentication-webview-error.txt");await File.WriteAllTextAsync(report,error.GetType().Name+" HRESULT "+error.HResult.ToString("X")+" "+error.Message);Shutdown(1);}return;}
  if(e.Args.Contains("--data-validate")){var data=await Task.Run(()=>logs.Read());var output=ApplicationPaths.ArtifactsDirectory;Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"real-data.json"),System.Text.Json.JsonSerializer.Serialize(data.Vendors.Select(v=>new{v.Title,v.Detection,Today=v.Today.Tokens.Total,Month=v.Month.Tokens.Total,AllTime=v.AllTime.Tokens.Total,v.SourceStatus}),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));Shutdown();return;}
  var model=new IslandViewModel();var source=new MockUsageSource();bool demo=e.Args.Any(a=>a is "--validate" or "--glass-validate" or "--glass-position-validate" or "--glass-review" or "--glass-rate-validate");
  Themes.ThemeManager.Apply(AppearanceTheme.Dark);
  if(demo)model.Apply(await source.GetUsageAsync(default));
  else{model.Configure(e.Args.Contains("--live-validate")?DisplayPreferences.Default:preferencesStore.Load());model.Select(model.FirstProvider);model.Apply(new(DateTimeOffset.Now,DateTimeOffset.Now,TimeZoneInfo.Local.Id,false,RefreshState.Refreshing,Array.Empty<ProviderUsageSnapshot>(),Array.Empty<ModelUsage>()));}
  var island=new IslandWindow(model,!e.Args.Contains("--validate")&&!e.Args.Contains("--live-validate"));MainWindow=island;
  island.Closed+=(_,_)=>{if(!e.Args.Any(arg=>arg is "--validate" or "--glass-validate" or "--glass-position-validate" or "--live-validate" or "--glass-rate-validate"))Shutdown();};
  if(e.Args.Contains("--review"))island.ShowInTaskbar=true;
  island.Show();NativeShow(island.Native!.Handle,4);
  if(e.Args.Contains("--review")){var style=TokNotch.Infrastructure.Windows.NativeMethods.GetWindowLongPtr(island.Native.Handle,-20);TokNotch.Infrastructure.Windows.NativeMethods.SetWindowLongPtr(island.Native.Handle,-20,style&~TokNotch.Infrastructure.Windows.NativeMethods.WsExToolWindow);island.Policy.SetMode(TokNotch.Core.Interaction.ExpansionMode.AlwaysExpanded);}
  tray=new();tray.ShowRequested+=()=>island.Policy.ShowExpanded();tray.ExitRequested+=Shutdown;
  if(!demo){var restoreKeys=!e.Args.Contains("--live-validate");if(restoreKeys&&sessionVault.Load() is {} saved)mimo.Configure(saved.Header);if(restoreKeys&&keyVault.Load() is {} keys){if(keys.DeepSeek is {} deepKey)deepSeek.Configure(deepKey);if(keys.Kimi is {} kimiKey)kimi.Configure(kimiKey);}liveModel=model;shell=island;ApplyRefreshPreferences(model.Preferences);ApplyWindowPreferences(model.Preferences);island.SettingsRequested+=()=>OpenSettings();tray.SettingsRequested+=()=>OpenSettings();tray.RefreshRequested+=()=>_=RefreshNowAsync();await Refresh();
   if(e.Args.Contains("--balance-validate")){await File.WriteAllTextAsync(Path.Combine(ProjectRoot,"artifacts","real-balances.json"),System.Text.Json.JsonSerializer.Serialize(model.Snapshot!.Vendors.Select(v=>new{v.Title,v.Balance,v.Currency,v.BalanceSource,v.BalanceStatus}),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));Shutdown();return;}
   if(e.Args.Contains("--live-validate")){try{await LiveValidation.RunAsync(island,model);await BalanceValidation.RunAsync(island,model);await SettingsValidation.RunAsync(model);await WindowSettingsValidation.RunAsync(island,model);await ElasticValidation.RunAsync(island,model);await RefreshValidation.RunAsync(model,_=>Refresh());}catch(Exception error){await File.WriteAllTextAsync(Path.Combine(ProjectRoot,"artifacts","live-validation-error.txt"),error.ToString());Shutdown(1);return;}Shutdown();return;}
   island.PositionChanged+=position=>{var next=model.Preferences.WithWindow(position);model.Configure(next);settingsWindow?.UpdatePositionEditor(position);try{preferencesStore.Save(next);}catch(Exception error)when(error is IOException or UnauthorizedAccessException){OpenSettings();settingsWindow!.ReportPositionSaveError();}};
   StartRefreshLoop();
  }
  if(e.Args.Contains("--settings"))OpenSettings(e.Args.Contains("--connections"));if(e.Args.Contains("--appearance"))settingsWindow?.ShowAppearancePage();if(e.Args.Contains("--mimo-login")){OpenSettings(true);_=LoginMimo();}
  if(e.Args.Contains("--glass-rate-validate")){try{await Glass.GlassFrameRateValidation.RunAsync(island);Shutdown();}catch(Exception error){await File.WriteAllTextAsync(Path.Combine(ProjectRoot,"artifacts","glass-rate-error.txt"),error.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--glass-position-validate")){try{await Glass.GlassValidation.RunPositionAsync(island);Shutdown();}catch(Exception error){await File.WriteAllTextAsync(Path.Combine(ProjectRoot,"artifacts","glass-position-error.txt"),error.ToString());Shutdown(1);}}
  if(e.Args.Contains("--glass-validate")){try{await Glass.GlassValidation.RunAsync(island);Shutdown();}catch(Exception error){await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory,"glass-validation-error.txt"),error.ToString());Shutdown(1);}}
  if(e.Args.Contains("--glass-review")){var background=Glass.GlassValidation.Background(island,true);island.Policy.SetMode(TokNotch.Core.Interaction.ExpansionMode.AlwaysExpanded);await Task.Delay(1500);background.Left=island.Left-50;await Task.Delay(700);island.Glass?.Dispose();background.Close();}
  if(e.Args.Contains("--validate")){try{await Validation.RunAsync(island,model,source);Shutdown();}catch(Exception error){await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory,"validation-error.txt"),error.ToString());Shutdown(1);}}
 }
 private void ActivateExisting(LaunchRequest request)
 {
  if(shell is null)return;
  shell.Show();NativeShow(shell.Native!.Handle,4);shell.Policy.ShowExpanded();
  if(request==LaunchRequest.Show)return;
  OpenSettings(request is LaunchRequest.Connections or LaunchRequest.MimoLogin);
  if(request==LaunchRequest.Appearance)settingsWindow?.ShowAppearancePage();
  if(request==LaunchRequest.MimoLogin)_=LoginMimo();
 }
 private void OpenSettings(bool connections=false)
 {
  if(liveModel==null)return;
  if(settingsWindow==null){settingsWindow=new(liveModel.Preferences,p=>{preferencesStore.Save(p);liveModel.Configure(p);ApplyRefreshPreferences(p);ApplyWindowPreferences(p);},ConnectKimiAsync,async cookie=>{mimo.Configure(cookie);await Refresh();return liveModel.Snapshot?.Vendors.FirstOrDefault(v=>v.Provider==Provider.Mimo)?.SourceStatus??"待刷新";},LoginMimo,DisconnectMimo,ConnectDeepSeekAsync,ForgetDeepSeekKey,ForgetKimiKey,new SettingsCommands(RefreshNowAsync,()=>shell?.Policy.ShowExpanded(),ConfirmExit));settingsWindow.Closed+=(_,_)=>settingsWindow=null;settingsWindow.Show();}
  if(connections)settingsWindow.ShowConnectionPage();settingsWindow.Activate();
 }
  private async Task<string> ConnectKimiAsync(string key)
  {
   kimi.Configure(key);await Refresh();
   var vendor=liveModel?.Snapshot?.Vendors.FirstOrDefault(v=>v.Provider==Provider.Kimi);var state=vendor?.SourceStatus??"待刷新";
   if(vendor?.Detection!=DetectionState.Ready)return state+" · 未保存，请核对后重试";
   try{keyVault.SetKimi(key);return state+" · 已加密保存，下次启动自动连接";}
   catch(Exception error)when(error is IOException or UnauthorizedAccessException){return state+" · 本次可用，但未能写入 config/api-keys.bin";}
  }
  private async Task<string> ConnectDeepSeekAsync(string key)
  {
   deepSeek.Configure(key);await Refresh();
   var vendor=liveModel?.Snapshot?.Vendors.FirstOrDefault(v=>v.Provider==Provider.DeepSeek);var state=vendor?.BalanceStatus??"待刷新";
   if(vendor?.Balance is null)return state+" · 未保存，请核对后重试";
   try{keyVault.SetDeepSeek(key);return state+" · 已加密保存，下次启动自动连接";}
   catch(Exception error)when(error is IOException or UnauthorizedAccessException){return state+" · 本次可用，但未能写入 config/api-keys.bin";}
  }
  private async Task<string> ForgetDeepSeekKey(){try{keyVault.SetDeepSeek(null);deepSeek.Configure(string.Empty);await Refresh();return "已清除保存的 DeepSeek key，下次启动需重新输入。";}catch(Exception error)when(error is IOException or UnauthorizedAccessException){return "清除失败，请检查项目目录是否可写。";}}
  private async Task<string> ForgetKimiKey(){try{keyVault.SetKimi(null);kimi.Configure(string.Empty);await Refresh();return "已清除保存的 Kimi key，下次启动需重新输入。";}catch(Exception error)when(error is IOException or UnauthorizedAccessException){return "清除失败，请检查项目目录是否可写。";}}
  private Task<string> LoginMimo()
 {
  if(loginWindow!=null){loginWindow.Activate();return loginTask!;}
  var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);loginTask=completion.Task;
  bool remembered=false;loginWindow=new(ApplicationPaths.DataRoot,async session=>{mimo.Configure(session.Header);await Refresh();if(liveModel?.Snapshot?.Vendors.FirstOrDefault(v=>v.Provider==Provider.Mimo)?.Detection!=DetectionState.Ready)return false;sessionVault.Save(session);remembered=true;return true;});
  if(settingsWindow!=null)loginWindow.Owner=settingsWindow;
  loginWindow.Closed+=(_,_)=>{loginWindow=null;completion.TrySetResult(remembered?"已自动连接 MiMo，启动时会恢复登录会话。":liveModel?.Snapshot?.Vendors.FirstOrDefault(v=>v.Provider==Provider.Mimo)?.Detection==DetectionState.Ready?"MiMo 已连接，本次会话尚未保存。":"尚未连接，完成登录后可重试。");};loginWindow.Show();return loginTask;
 }
 private async Task<string> DisconnectMimo(){try{sessionVault.Clear();mimo.Disconnect();loginWindow?.Close();await Refresh();return "已断开自动连接。再次登录可重新连接。";}catch(IOException){return "暂时无法清除保存的会话，请重试。";}catch(UnauthorizedAccessException){return "暂时无法清除保存的会话，请重试。";}}
 private void StartRefreshLoop(){scheduler=new RefreshScheduler(_=>Refresh()){Interval=TimeSpan.FromSeconds(liveModel?.Preferences.RefreshSeconds??30)};_=scheduler.Start(stop.Token);}
 /// <summary>Applies the saved intervals to the sources and to the running loop; the loop re-waits at once instead of finishing the old, possibly long, interval.</summary>
 private void ApplyRefreshPreferences(DisplayPreferences preferences)
 {
  kimi.Interval=mimo.Interval=deepSeek.Interval=TimeSpan.FromSeconds(preferences.BalanceRefreshSeconds);
  if(scheduler is null)return;
  scheduler.Interval=TimeSpan.FromSeconds(preferences.RefreshSeconds);scheduler.Reschedule();
 }
 /// <summary>Manual refresh from the tray or the settings page: forces the balances and reports the moment.</summary>
 private async Task<string> RefreshNowAsync(){kimi.RequestRefresh();mimo.RequestRefresh();deepSeek.RequestRefresh();await Refresh();return $"已刷新 {DateTimeOffset.Now:HH:mm:ss}";}
 private void ConfirmExit(){if(MessageBox.Show("退出 TokNotch？","TokNotch",MessageBoxButton.OKCancel,MessageBoxImage.Question)==MessageBoxResult.OK)Shutdown();}
 /// <summary>Expansion mode and material are saved preferences now; applied at startup and on every save.</summary>
 private void ApplyWindowPreferences(DisplayPreferences preferences)
 {
  if(shell is null)return;
  shell.ApplyPreferences(preferences);
 }
 private async Task Refresh()
 {
  if(liveModel==null||stop.IsCancellationRequested)return;
  try{await refreshGate.WaitAsync(stop.Token);}catch(OperationCanceledException){return;}
  try{var local=await Task.Run(()=>logs.Read(stop.Token),stop.Token);var remote=await Task.WhenAll(kimi.ReadAsync(stop.Token),mimo.ReadAsync(stop.Token),deepSeek.ReadAsync(local.Vendors.Single(v=>v.Provider==Provider.DeepSeek),stop.Token));var account=remote.Single(r=>r.Provider==Provider.DeepSeek);var vendors=local.Vendors.Where(v=>v.Provider!=Provider.DeepSeek).Select(v=>v.Provider==Provider.Dsh?DshBalanceBinding.Attach(v,account):v);liveModel.Apply(new(local.GeneratedAt,local.LastSuccessAt,local.BucketTimeZone,false,local.RefreshState,vendors.Concat(remote),local.Models));}
  catch(OperationCanceledException){}
  catch(Exception){if(liveModel.Snapshot is {} last)liveModel.Apply(new(last.GeneratedAt,last.LastSuccessAt,last.BucketTimeZone,false,RefreshState.Failed,last.Vendors,last.Models,"刷新失败 · 显示上次数据"));}
  finally{refreshGate.Release();}
 }
 protected override void OnExit(ExitEventArgs e){stop.Cancel();loginWindow?.Close();settingsWindow?.Close();kimi.Dispose();mimo.Dispose();deepSeek.Dispose();tray?.Dispose();singleInstance?.Dispose();base.OnExit(e);}
 [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="ShowWindow")]
 private static extern bool NativeShow(IntPtr handle,int command);
}
