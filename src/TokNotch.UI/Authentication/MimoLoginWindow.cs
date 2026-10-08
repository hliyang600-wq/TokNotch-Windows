using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TokNotch.Infrastructure.Authentication;
namespace TokNotch.UI.Authentication;
internal sealed class MimoLoginWindow : Window
{
 private readonly WebView2 browser=new();
 private readonly TextBlock status=new(){Text="正在打开小米登录页…",FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(20,12,20,12)};
 private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};
 private readonly string profile;
 private readonly Func<MimoSession,Task<bool>> connected;
 private readonly bool qwen;private string PlatformName=>qwen?"千问":"MiMo";
 private bool checking,closed;
 public MimoLoginWindow(string projectRoot,Func<MimoSession,Task<bool>> connect,bool qwen=false)
 {
  this.qwen=qwen;profile=Path.Combine(projectRoot,"config",qwen?"qwen-webview":"mimo-webview");connected=connect;
  if(qwen){status.Text="正在打开千问登录页…";timer.Interval=TimeSpan.FromSeconds(5);}
  Title=$"登录 {PlatformName} · 自动连接";Width=900;Height=700;MinWidth=640;MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=new SolidColorBrush(Color.FromRgb(23,24,30));
  var layout=new DockPanel();Content=layout;var footer=new DockPanel{LastChildFill=true};var retry=new Button{Content="重新检查",Padding=new Thickness(12,6,12,6),Margin=new Thickness(8,10,16,10)};DockPanel.SetDock(retry,Dock.Right);footer.Children.Add(retry);footer.Children.Add(status);retry.Click+=async(_,_)=>await CheckLogin();DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(browser);
  Loaded+=async(_,_)=>await Initialize();timer.Tick+=async(_,_)=>await CheckLogin();
  Closed+=(_,_)=>{closed=true;timer.Stop();browser.Dispose();};
 }
 private async Task Initialize()
 {
  try{
   Directory.CreateDirectory(profile);var environment=await CoreWebView2Environment.CreateAsync(null,profile);if(closed)return;await browser.EnsureCoreWebView2Async(environment);if(closed)return;
   browser.CoreWebView2.Settings.AreDevToolsEnabled=false;browser.CoreWebView2.Settings.IsStatusBarEnabled=false;browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled=false;browser.CoreWebView2.Settings.IsGeneralAutofillEnabled=false;
   browser.CoreWebView2.NavigationStarting+=(_,args)=>{if(!AllowedNavigation(args.Uri)){args.Cancel=true;status.Text=$"请在 {PlatformName} 官方登录页完成登录。";}};
   browser.CoreWebView2.NewWindowRequested+=(_,args)=>{args.Handled=true;if(AllowedNavigation(args.Uri))browser.CoreWebView2.Navigate(args.Uri);};
   browser.CoreWebView2.DownloadStarting+=(_,args)=>args.Cancel=true;
   browser.CoreWebView2.NavigationCompleted+=async(_,_)=>await CheckLogin();
   status.Text=$"在此登录 {PlatformName} 账号，成功后自动连接，无需复制 Cookie。";
   browser.CoreWebView2.Navigate(qwen?"https://platform.qianwenai.com/home/analytics/token-plan/individual":MimoCookiePolicy.Origin+"/console/usage");timer.Start();
  }catch(Exception e)when(e is Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or InvalidOperationException){if(!closed)status.Text="内置登录页暂不可用，请重试或使用设置中的手动 Cookie 连接。";}
 }
 private async Task CheckLogin()
 {
  if(checking||closed||browser.CoreWebView2==null)return;checking=true;
  try{
   var session=qwen?await CaptureQwenAsync(browser.CoreWebView2):await CaptureAsync(browser.CoreWebView2);if(session==null||closed)return;
   if(!qwen)timer.Stop();status.Text="正在检查登录与套餐余量…";
   if(await connected(session)){timer.Stop();if(!closed)Close();}
   else if(!closed){status.Text=qwen?"请完成千问登录；有个人版套餐后会自动连接，也可点“重新检查”。":"会话已取得，但用量查询未成功。请完成登录后点“重新检查”重试。";}
  }catch(Exception e)when(e is System.Runtime.InteropServices.COMException or InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Security.Cryptography.CryptographicException or OperationCanceledException or ArgumentException){if(!closed)status.Text="读取或保存会话暂时失败，请重新登录或关闭后重试。";}
  finally{checking=false;}
 }
 internal static async Task<MimoSession?> CaptureAsync(CoreWebView2 core)
 {
  var cookies=await core.CookieManager.GetCookiesAsync(MimoCookiePolicy.UsageUri);var rows=cookies.Select(c=>new MimoCookie(c.Name,c.Value,c.Domain,c.Path,c.IsSecure,c.IsSession?null:new DateTimeOffset(c.Expires.ToUniversalTime())));return MimoCookiePolicy.Extract(rows,DateTimeOffset.UtcNow);
 }
 private bool AllowedNavigation(string url)=>qwen?AllowedQwenNavigation(url):MimoCookiePolicy.IsLoginNavigation(url);
 internal static bool AllowedQwenNavigation(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&new[]{"qianwenai.com","aliyun.com","alipay.com"}.Any(domain=>uri.Host==domain||uri.Host.EndsWith("."+domain,StringComparison.OrdinalIgnoreCase));
 internal static async Task<MimoSession?> CaptureQwenAsync(CoreWebView2 core)
 {
  var values=new Dictionary<string,string>(StringComparer.Ordinal);var now=DateTimeOffset.UtcNow;
  foreach(var url in new[]{"https://platform-home.qianwenai.com/tool/user/info.json","https://cs-data.qianwenai.com/data/api.json"}){
   foreach(var cookie in await core.CookieManager.GetCookiesAsync(url)){
    var domain=cookie.Domain.TrimStart('.');if(!(domain=="qianwenai.com"||domain.EndsWith(".qianwenai.com",StringComparison.OrdinalIgnoreCase))||!cookie.IsSession&&cookie.Expires.ToUniversalTime()<=now.UtcDateTime)continue;
    if(cookie.Name.Any(ch=>ch is '\r' or '\n' or ';' or '=')||cookie.Value.Any(ch=>ch is '\r' or '\n' or ';'))continue;values.TryAdd(cookie.Name,cookie.Value);
   }
  }
  return values.Count==0?null:new(string.Join("; ",values.Select(pair=>pair.Key+"="+pair.Value)),now,now.AddDays(1));
 }
}
