using System.Net;using System.Text;using System.Text.Json;
using TokNotch.Infrastructure.Authentication;using TokNotch.Infrastructure.Usage;using TokNotch.Core.Models;
internal static class AuthenticationTests
{
 public static async Task Run(string root)
 {
  int count=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
  var now=DateTimeOffset.UtcNow;var rows=new[]{
   new MimoCookie("api-platform_serviceToken","fixture-session",".platform.xiaomimimo.com","/",true,now.AddHours(24)),new("userId","123",".xiaomimimo.com","/",true,null),new("api-platform_ph","fixture-ph","platform.xiaomimimo.com","/",false,null),
   new("passToken","must-not-export",".xiaomi.com","/",true,null),new("api-platform_slh","wrong-domain",".example.com","/",true,null),new("api-platform_slh","wrong-path","platform.xiaomimimo.com","/private",true,null),new("api-platform_slh","expired","platform.xiaomimimo.com","/",true,now.AddDays(-1))};
  var session=MimoCookiePolicy.Extract(rows,now)!;Check(session!=null&&session.Header.Contains("fixture-session")&&session.Header.Contains("fixture-ph"),"MiMo cookie extraction handles secure and normal HTTPS cookies");Check(!session!.Header.Contains("must-not-export")&&!session.Header.Contains("wrong-domain")&&!session.Header.Contains("wrong-path")&&!session.Header.Contains("expired"),"unrelated, wrong-domain, wrong-path and expired cookies excluded");
  Check(MimoCookiePolicy.Extract(rows.Where(r=>r.Name!="api-platform_serviceToken"),now)==null,"logged-out cookie set not treated as login");Check(MimoCookiePolicy.IsLoginNavigation("https://account.xiaomi.com/pass/serviceLogin")&&MimoCookiePolicy.IsLoginNavigation("https://platform.xiaomimimo.com/console/usage")&&!MimoCookiePolicy.IsLoginNavigation("http://platform.xiaomimimo.com/")&&!MimoCookiePolicy.IsLoginNavigation("https://xiaomi.com.evil.example/"),"official HTTPS login domain restrictions");
  var folder=Path.Combine(root,"auth-test");var vault=new MimoSessionVault(folder);vault.Save(session);var restored=new MimoSessionVault(folder).Load();Check(restored?.Header==session.Header,"DPAPI session survives restart");var file=Path.Combine(folder,"config","mimo-session.bin");var bytes=File.ReadAllBytes(file);Check(!Encoding.UTF8.GetString(bytes).Contains("fixture-session"),"cookie is absent from saved plaintext");
  using(var source=new MimoUsageSource(new HttpClient(new Handler()))){source.Configure(restored!.Header);Check((await source.ReadAsync(default)).Detection==DetectionState.Ready,"restored automatic session connects usage without browser");}
  bytes[^1]^=1;File.WriteAllBytes(file,bytes);Check(vault.Load()==null,"tampered protected session safely ignored");vault.Save(session with {ExpiresAt=now.AddDays(-1)});Check(vault.Load()==null,"expired saved session not auto-restored");vault.Save(session);vault.Clear();Check(vault.Load()==null&&Directory.GetFiles(Path.Combine(folder,"backups","mimo-session")).Length>0,"disconnect clears active auto-login and keeps encrypted backup");Console.WriteLine($"{count} automatic authentication checks passed");
 }
 private sealed class Handler:HttpMessageHandler
 {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   ct.ThrowIfCancellationRequested();if(request.RequestUri!.Host!="platform.xiaomimimo.com"||!request.Headers.GetValues("Cookie").Single().Contains("fixture-session"))throw new Exception("Unexpected cookie destination");
   var data=request.RequestUri.AbsolutePath=="/api/v1/balance"?"{\"balance\":\"23.45\",\"currency\":\"CNY\"}":request.Method==HttpMethod.Get?"{\"tokenUsage\":{\"inputToken\":90,\"outputToken\":10,\"cacheToken\":20,\"totalToken\":100}}":JsonSerializer.Serialize(new[]{new{date=DateTime.Now.ToString("yyyy-MM-dd"),inputMissToken=70,outputToken=10,inputHitToken=20,totalToken=100}});
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"code\":0,\"data\":"+data+"}")});
  }
 }
}
