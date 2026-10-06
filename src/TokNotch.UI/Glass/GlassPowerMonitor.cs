using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace TokNotch.UI.Glass;

// Event subscriptions only: no system power settings or session state are changed.
internal sealed class GlassPowerMonitor : IDisposable
{
 private readonly IntPtr hwnd;
 private readonly HwndSource source;
 private readonly Action<bool> changed;
 private readonly IntPtr notification;
 private readonly bool sessionRegistered;
 private bool locked,displayOff,sleeping;
 private static readonly Guid DisplayState=new("2B84C20E-AD23-4ddf-93DB-05FFBD7EFCA5");
 internal GlassPowerMonitor(IntPtr hwnd,Action<bool> changed)
 {
  this.hwnd=hwnd;this.changed=changed;source=HwndSource.FromHwnd(hwnd);
  source.AddHook(Hook);sessionRegistered=WTSRegisterSessionNotification(hwnd,0);
  var guid=DisplayState;notification=RegisterPowerSettingNotification(hwnd,ref guid,0);
 }
 private IntPtr Hook(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
 {
  if(message==0x2B1) {
   if(wParam.ToInt32()==7)locked=true;else if(wParam.ToInt32()==8)locked=false;else return IntPtr.Zero;
  }else if(message==0x218) {
   if(wParam.ToInt32()==4)sleeping=true;
   else if(wParam.ToInt32() is 7 or 18)sleeping=false;
   else if(wParam.ToInt32()==0x8013&&lParam!=IntPtr.Zero&&Marshal.PtrToStructure<Guid>(lParam)==DisplayState&&Marshal.ReadInt32(lParam,16)==4)displayOff=Marshal.ReadInt32(lParam,20)==0;
   else return IntPtr.Zero;
  }else return IntPtr.Zero;
  changed(locked||displayOff||sleeping);return IntPtr.Zero;
 }
 public void Dispose()
 {
  source.RemoveHook(Hook);
  if(notification!=IntPtr.Zero)UnregisterPowerSettingNotification(notification);
  if(sessionRegistered)WTSUnRegisterSessionNotification(hwnd);
 }
 [DllImport("user32.dll")] private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient,ref Guid setting,uint flags);
 [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
 [DllImport("wtsapi32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool WTSRegisterSessionNotification(IntPtr hwnd,uint flags);
 [DllImport("wtsapi32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
}
