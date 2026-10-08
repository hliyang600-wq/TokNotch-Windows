using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace TokNotch.Infrastructure.Authentication;

/// <summary>Provider keys and the optional Qwen console Cookie in one protected payload.</summary>
public sealed record SavedApiKeys(string? DeepSeek,string? Kimi,string? QwenCookie=null)
{
 public bool IsEmpty=>string.IsNullOrWhiteSpace(DeepSeek)&&string.IsNullOrWhiteSpace(Kimi)&&string.IsNullOrWhiteSpace(QwenCookie);
}

/// <summary>
/// DPAPI current-user protection for provider credentials, using the same mechanism as the MiMo session vault.
/// The stored file never contains a readable key and is excluded from Git, but any process running as this Windows user
/// can decrypt it, so the settings page can clear it at any time.
/// </summary>
public sealed class ApiKeyVault(string projectRoot)
{
 private const int MaxKeyLength=8192;
 private readonly string file=Path.Combine(projectRoot,"config","api-keys.bin");
 public string FilePath=>file;
 public SavedApiKeys? Load()
 {
  if(!File.Exists(file))return null;
  try
  {
   if(new FileInfo(file).Length>65536)return null;
   var plain=Transform(File.ReadAllBytes(file),false);
   try
   {
    var saved=JsonSerializer.Deserialize<SavedApiKeys>(plain);
    if(saved is null||saved.IsEmpty)return null;
    var restored=new SavedApiKeys(Valid(saved.DeepSeek),Valid(saved.Kimi),Valid(saved.QwenCookie));
    return restored.IsEmpty?null:restored;
   }
   finally{CryptographicOperations.ZeroMemory(plain);}
  }
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or Win32Exception or JsonException or CryptographicException){return null;}
 }
 /// <summary>Stores one key while leaving the other provider untouched. A blank key clears only that provider; a malformed key is rejected instead of silently wiping it.</summary>
 public void SetDeepSeek(string? key){var saved=Load();Write(new(Require(key),saved?.Kimi,saved?.QwenCookie));}
 public void SetKimi(string? key){var saved=Load();Write(new(saved?.DeepSeek,Require(key),saved?.QwenCookie));}
 public void SetQwenCookie(string? cookie){var saved=Load();Write(new(saved?.DeepSeek,saved?.Kimi,Require(cookie)));}
 public void Clear(){if(File.Exists(file)){Backup();File.Delete(file);}}
 private void Write(SavedApiKeys keys)
 {
  if(keys.IsEmpty){Clear();return;}
  var plain=JsonSerializer.SerializeToUtf8Bytes(keys);byte[] encrypted;try{encrypted=Transform(plain,true);}finally{CryptographicOperations.ZeroMemory(plain);}
  Directory.CreateDirectory(Path.GetDirectoryName(file)!);var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllBytes(temp,encrypted);Backup();File.Move(temp,file,true);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
 private void Backup(){if(!File.Exists(file))return;var folder=Path.Combine(projectRoot,"backups","api-keys");Directory.CreateDirectory(folder);File.Copy(file,Path.Combine(folder,"keys-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff")+".bin"));}
 /// <summary>Lenient check for untrusted file content; a malformed stored field is dropped rather than trusted.</summary>
 private static string? Valid(string? key)=>string.IsNullOrWhiteSpace(key)||key.Length>MaxKeyLength||key.Contains('\r')||key.Contains('\n')?null:key.Trim();
 /// <summary>Strict check for keys coming from the user: blank means clear, malformed means reject.</summary>
 private static string? Require(string? key)
 {
  if(string.IsNullOrWhiteSpace(key))return null;
  if(key.Length>MaxKeyLength||key.Contains('\r')||key.Contains('\n'))throw new ArgumentException("API key 格式无效");
  return key.Trim();
 }
 private static byte[] Transform(byte[] bytes,bool protect)
 {
  var input=Allocate(bytes);var entropy=Allocate(Encoding.UTF8.GetBytes("TokNotch.ApiKeys.v1"));var output=new Blob();IntPtr description=IntPtr.Zero;
  try{bool ok=protect?CryptProtectData(ref input,"TokNotch API keys",ref entropy,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,out description,ref entropy,IntPtr.Zero,IntPtr.Zero,1,out output);if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error());var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;}
  finally{WipeFree(input,false);WipeFree(entropy,false);WipeFree(output,true);if(description!=IntPtr.Zero)LocalFree(description);}
 }
 private static Blob Allocate(byte[] bytes){var blob=new Blob{Size=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Marshal.Copy(bytes,0,blob.Data,bytes.Length);return blob;}
 private static void WipeFree(Blob blob,bool local){if(blob.Data==IntPtr.Zero)return;if(blob.Size>0)Marshal.Copy(new byte[blob.Size],0,blob.Data,blob.Size);if(local)LocalFree(blob.Data);else Marshal.FreeHGlobal(blob.Data);}
 [StructLayout(LayoutKind.Sequential)]private struct Blob{public int Size;public IntPtr Data;}
 [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CryptProtectData(ref Blob input,string description,ref Blob entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
 [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CryptUnprotectData(ref Blob input,out IntPtr description,ref Blob entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
 [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr memory);
}
