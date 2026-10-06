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
 private bool checking,closed;
 public MimoLoginWindow(string projectRoot,Func<MimoSession,Task<bool>> connect)
 {
  profile=Path.Combine(projectRoot,"config","mimo-webview");connected=connect;
  Title="登录 MiMo · 自动连接";Width=900;Height=700;MinWidth=640;MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=new SolidColorBrush(Color.FromRgb(23,24,30));
  var layout=new DockPanel();Content=layout;var footer=new DockPanel{LastChildFill=true};var retry=new Button{Content="重新检查",Padding=new Thickness(12,6,12,6),Margin=new Thickness(8,10,16,10)};DockPanel.SetDock(retry,Dock.Right);footer.Children.Add(retry);footer.Children.Add(status);retry.Click+=async(_,_)=>await CheckLogin();DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(browser);
  Loaded+=async(_,_)=>await Initialize();timer.Tick+=async(_,_)=>await CheckLogin();
  Closed+=(_,_)=>{closed=true;timer.Stop();browser.Dispose();};
 }
 private async Task Initialize()
 {
  try{
   Directory.CreateDirectory(profile);var environment=await CoreWebView2Environment.CreateAsync(null,profile);if(closed)return;await browser.EnsureCoreWebView2Async(environment);if(closed)return;
   browser.CoreWebView2.Settings.AreDevToolsEnabled=false;browser.CoreWebView2.Settings.IsStatusBarEnabled=false;browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled=false;browser.CoreWebView2.Settings.IsGeneralAutofillEnabled=false;
   browser.CoreWebView2.NavigationStarting+=(_,args)=>{if(!MimoCookiePolicy.IsLoginNavigation(args.Uri)){args.Cancel=true;status.Text="请在小米官方登录页完成登录。";}};
   browser.CoreWebView2.NewWindowRequested+=(_,args)=>{args.Handled=true;if(MimoCookiePolicy.IsLoginNavigation(args.Uri))browser.CoreWebView2.Navigate(args.Uri);};
   browser.CoreWebView2.DownloadStarting+=(_,args)=>args.Cancel=true;
   browser.CoreWebView2.NavigationCompleted+=async(_,_)=>await CheckLogin();
   status.Text="在此登录小米账号，成功后会自动读取 MiMo 会话并连接，无需复制 Cookie。";
   browser.CoreWebView2.Navigate(MimoCookiePolicy.Origin+"/console/usage");timer.Start();
  }catch(Exception e)when(e is Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or InvalidOperationException){if(!closed)status.Text="内置登录页暂不可用，请重试或使用设置中的手动 Cookie 连接。";}
 }
 private async Task CheckLogin()
 {
  if(checking||closed||browser.CoreWebView2==null)return;checking=true;
  try{
   var session=await CaptureAsync(browser.CoreWebView2);if(session==null||closed)return;
   timer.Stop();status.Text="已取得登录会话，正在核对用量…";
   if(await connected(session)){if(!closed)Close();}
   else if(!closed){status.Text="会话已取得，但用量查询未成功。请完成登录后点“重新检查”重试。";}
  }catch(Exception e)when(e is System.Runtime.InteropServices.COMException or InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Security.Cryptography.CryptographicException){if(!closed)status.Text="读取或保存会话暂时失败，请重新登录或关闭后重试。";}
  finally{checking=false;}
 }
 internal static async Task<MimoSession?> CaptureAsync(CoreWebView2 core)
 {
  var cookies=await core.CookieManager.GetCookiesAsync(MimoCookiePolicy.UsageUri);var rows=cookies.Select(c=>new MimoCookie(c.Name,c.Value,c.Domain,c.Path,c.IsSecure,c.IsSession?null:new DateTimeOffset(c.Expires.ToUniversalTime())));return MimoCookiePolicy.Extract(rows,DateTimeOffset.UtcNow);
 }
}



