using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TokNotch.Infrastructure.Authentication;
namespace TokNotch.UI.Authentication;
internal static class AuthenticationValidation
{
 public static async Task RunAsync()
 {
  var output=ApplicationPaths.ArtifactsDirectory;Directory.CreateDirectory(output);var checks=new List<string>();void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
  using var browser=new WebView2();var host=new Window{Title="TokNotch 登录验证",Width=400,Height=240,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false,Content=browser};host.Show();
  try{
   var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(output,"webview-auth-fixture"));await browser.EnsureCoreWebView2Async(environment);var core=browser.CoreWebView2;core.CookieManager.DeleteAllCookies();await Task.Delay(300);
   var cookie=core.CookieManager.CreateCookie("api-platform_serviceToken","fixture-session","platform.xiaomimimo.com","/");cookie.IsSecure=true;cookie.IsHttpOnly=true;cookie.Expires=DateTime.UtcNow.AddHours(1);core.CookieManager.AddOrUpdateCookie(cookie);
   var other=core.CookieManager.CreateCookie("passToken","unrelated-fixture","account.xiaomi.com","/");other.IsSecure=true;core.CookieManager.AddOrUpdateCookie(other);
   var session=await MimoLoginWindow.CaptureAsync(core);Check(session!=null&&session.Header.Contains("fixture-session"),"actual WebView2 CookieManager captures HttpOnly MiMo session");Check(!session!.Header.Contains("unrelated-fixture"),"login-provider cookies never exported to MiMo query");
   core.CookieManager.DeleteCookies("api-platform_serviceToken",MimoCookiePolicy.UsageUri);await Task.Delay(300);Check(await MimoLoginWindow.CaptureAsync(core)==null,"logged-out WebView2 profile stays disconnected");core.CookieManager.DeleteCookies("passToken","https://account.xiaomi.com/");
   var qwen=core.CookieManager.CreateCookie("session","qwen-fixture",".qianwenai.com","/");qwen.IsSecure=true;qwen.IsHttpOnly=true;qwen.Expires=DateTime.UtcNow.AddHours(1);core.CookieManager.AddOrUpdateCookie(qwen);
   var foreign=core.CookieManager.CreateCookie("login-session","must-not-export","account.aliyun.com","/");foreign.IsSecure=true;core.CookieManager.AddOrUpdateCookie(foreign);
   var captured=await MimoLoginWindow.CaptureQwenAsync(core);Check(captured?.Header.Contains("qwen-fixture")==true&&!captured.Header.Contains("must-not-export"),"actual WebView2 captures HttpOnly Qwen session without exporting Aliyun login-provider cookies");
   Check(MimoLoginWindow.AllowedQwenNavigation("https://signin.aliyun.com/")&&MimoLoginWindow.AllowedQwenNavigation("https://platform.qianwenai.com/")&&!MimoLoginWindow.AllowedQwenNavigation("https://qianwenai.com.evil.example/")&&!MimoLoginWindow.AllowedQwenNavigation("http://platform.qianwenai.com/"),"Qwen login restricts main navigation to official HTTPS domains");
   foreach(var saved in await core.CookieManager.GetCookiesAsync("https://cs-data.qianwenai.com/")){if(saved.Name=="session")core.CookieManager.DeleteCookie(saved);}await Task.Delay(300);Check(await MimoLoginWindow.CaptureQwenAsync(core)==null,"empty Qwen WebView2 profile never invents a saved session");core.CookieManager.DeleteCookies("login-session","https://account.aliyun.com/");
  }finally{host.Close();}
  await File.WriteAllLinesAsync(Path.Combine(output,"authentication-webview-checks.txt"),checks);
 }
}
