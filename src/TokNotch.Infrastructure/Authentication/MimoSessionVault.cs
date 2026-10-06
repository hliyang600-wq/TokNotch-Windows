using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace TokNotch.Infrastructure.Authentication;
/// <summary>DPAPI current-user protection. This file never contains a plaintext Cookie.</summary>
public sealed class MimoSessionVault(string projectRoot)
{
 private readonly string file=Path.Combine(projectRoot,"config","mimo-session.bin");
 public MimoSession? Load()
 {
  if(!File.Exists(file))return null;
  try{if(new FileInfo(file).Length>65536)return null;var plain=Transform(File.ReadAllBytes(file),false);try{var session=JsonSerializer.Deserialize<MimoSession>(plain);return session is null||string.IsNullOrWhiteSpace(session.Header)||session.Header.Contains('\r')||session.Header.Contains('\n')||session.ExpiresAt<=DateTimeOffset.UtcNow?null:session;}finally{CryptographicOperations.ZeroMemory(plain);}}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or Win32Exception or JsonException or CryptographicException){return null;}
 }
 public void Save(MimoSession session)
 {
  var plain=JsonSerializer.SerializeToUtf8Bytes(session);byte[] encrypted;try{encrypted=Transform(plain,true);}finally{CryptographicOperations.ZeroMemory(plain);}
  Directory.CreateDirectory(Path.GetDirectoryName(file)!);var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllBytes(temp,encrypted);Backup();File.Move(temp,file,true);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public void Clear(){if(File.Exists(file)){Backup();File.Delete(file);}}
 private void Backup(){if(!File.Exists(file))return;var folder=Path.Combine(projectRoot,"backups","mimo-session");Directory.CreateDirectory(folder);File.Copy(file,Path.Combine(folder,"session-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff")+".bin"));}
 private static byte[] Transform(byte[] bytes,bool protect)
 {
  var input=Allocate(bytes);var entropy=Allocate(Encoding.UTF8.GetBytes("TokNotch.MiMo.Session.v1"));var output=new Blob();IntPtr description=IntPtr.Zero;
  try{bool ok=protect?CryptProtectData(ref input,"TokNotch MiMo",ref entropy,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,out description,ref entropy,IntPtr.Zero,IntPtr.Zero,1,out output);if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error());var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;}
  finally{WipeFree(input,false);WipeFree(entropy,false);WipeFree(output,true);if(description!=IntPtr.Zero)LocalFree(description);}
 }
 private static Blob Allocate(byte[] bytes){var blob=new Blob{Size=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Marshal.Copy(bytes,0,blob.Data,bytes.Length);return blob;}
 private static void WipeFree(Blob blob,bool local){if(blob.Data==IntPtr.Zero)return;if(blob.Size>0)Marshal.Copy(new byte[blob.Size],0,blob.Data,blob.Size);if(local)LocalFree(blob.Data);else Marshal.FreeHGlobal(blob.Data);}
 [StructLayout(LayoutKind.Sequential)]private struct Blob{public int Size;public IntPtr Data;}
 [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CryptProtectData(ref Blob input,string description,ref Blob entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
 [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CryptUnprotectData(ref Blob input,out IntPtr description,ref Blob entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
 [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr memory);
}
