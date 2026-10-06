using System.Text;
using TokNotch.Infrastructure.Authentication;
internal static class ApiKeyTests
{
 /// <summary>The DeepSeek/Kimi keys must survive a restart without ever being readable on disk, mirroring the MiMo session vault.</summary>
 public static async Task Run(string root)
 {
  int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
  var home=Path.Combine(root,"api-keys");if(Directory.Exists(home))Directory.Delete(home,true);Directory.CreateDirectory(home);
  var vault=new ApiKeyVault(home);
  Check(vault.Load() is null,"no saved file means nothing to restore");
  vault.SetDeepSeek("sk-deepseek-fixture-key");vault.SetKimi("sk-kimi-fixture-key");
  var restored=new ApiKeyVault(home).Load();
  Check(restored?.DeepSeek=="sk-deepseek-fixture-key"&&restored.Kimi=="sk-kimi-fixture-key","both API keys survive a restart");
  var file=Path.Combine(home,"config","api-keys.bin");var bytes=File.ReadAllBytes(file);
  var raw=Encoding.UTF8.GetString(bytes);
  Check(!raw.Contains("fixture-key"),"neither key appears as plaintext in the protected file");
  Check(bytes.Length>0&&bytes[0]!=0x7b,"protected payload is not raw JSON");
  bytes[^1]^=1;File.WriteAllBytes(file,bytes);
  Check(new ApiKeyVault(home).Load() is null,"tampered file is ignored instead of throwing");
  vault.SetDeepSeek("sk-deepseek-fixture-key");vault.SetKimi("sk-kimi-fixture-key");
  vault.SetDeepSeek("sk-deepseek-second");var afterDeep=new ApiKeyVault(home).Load();
  Check(afterDeep is not null&&afterDeep.DeepSeek=="sk-deepseek-second"&&afterDeep.Kimi=="sk-kimi-fixture-key","saving DeepSeek leaves the stored Kimi key untouched");
  vault.SetDeepSeek(null);var afterClearOne=new ApiKeyVault(home).Load();
  Check(afterClearOne is not null&&afterClearOne.DeepSeek is null&&afterClearOne.Kimi=="sk-kimi-fixture-key","clearing DeepSeek keeps Kimi");
  vault.SetKimi(null);Check(new ApiKeyVault(home).Load() is null&&!File.Exists(file),"clearing the last key removes the file");
  Check(Directory.GetFiles(Path.Combine(home,"backups","api-keys")).Length>0,"clearing keeps an encrypted backup");
  bool rejected=false;try{vault.SetDeepSeek("sk-fixture\r\nInjected: value");}catch(ArgumentException){rejected=true;}Check(rejected,"newline key rejected before it can be stored");
  rejected=false;try{vault.SetKimi(new string('k',9000));}catch(ArgumentException){rejected=true;}Check(rejected,"oversized key rejected before it can be stored");
  vault.SetDeepSeek("sk-deepseek-fixture-key");Check(new ApiKeyVault(home).Load()?.Kimi is null,"a single stored key does not invent the other provider");
  vault.Clear();Check(new ApiKeyVault(home).Load() is null,"clear removes every stored key");
  await Task.CompletedTask;
  Console.WriteLine($"{checks} API key vault checks passed");
 }
}
