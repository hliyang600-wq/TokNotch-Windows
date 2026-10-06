using System.Runtime.InteropServices;
using System.Threading;
namespace TokNotch.UI.Glass;

// DXGI Desktop Duplication. Copy only the island rectangle to CPU-readable memory.
// Frames stay in memory; no desktop images are persisted or sent over the network.
internal sealed class DesktopBackdrop : IDisposable
{
 private IntPtr device, context, duplication, staging;
 private int width,height;
 private IntPtr info=Marshal.AllocHGlobal(256);
 private RectI outputBounds;
 private byte[] pixels=Array.Empty<byte>();
 private byte[] previous=Array.Empty<byte>();
 private string outputDevice="";
 private RectI lastBounds;
 private Acquire acquire=null!;
 private ReleaseFrame releaseFrame=null!;
 private CopyRegion copyRegion=null!;
 private Map map=null!;
 private Unmap unmap=null!;
 internal int Readbacks { get; private set; }
 internal int TextureAllocations { get; private set; }
 internal int SkippedFrames { get; private set; }
 public DesktopBackdrop(IntPtr hwnd)
 {
  try { Initialize(hwnd); } catch { Dispose(); throw; }
 }
 private void Initialize(IntPtr hwnd)
 {
  if(!GetWindowRect(hwnd,out var bounds))throw new InvalidOperationException("Cannot obtain island bounds.");
  outputDevice=new TokNotch.Infrastructure.Windows.MonitorService().AtPoint((bounds.Left+bounds.Right)/2,(bounds.Top+bounds.Bottom)/2).Device;
  IntPtr factory=IntPtr.Zero, adapter=IntPtr.Zero, output=IntPtr.Zero, output1=IntPtr.Zero;
  try {
   var iid=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");Check(CreateDXGIFactory1(in iid,out factory),"CreateDXGIFactory1");
   bool found=false;
   for(uint a=0;!found;a++) {
    int hr=Call<EnumOutput>(factory,12)(factory,a,out adapter);Check(hr,"EnumAdapters1");
    for(uint i=0;;i++) {
     hr=Call<EnumOutput>(adapter,7)(adapter,i,out output);if(hr==unchecked((int)0x887A0002))break;Check(hr);
     Check(Call<GetOutputDesc>(output,7)(output,out var description));
     if(description.AttachedToDesktop!=0&&description.DeviceName==outputDevice) {if(description.Rotation>1)throw new NotSupportedException("Rotated displays require coordinate remapping.");outputBounds=description.DesktopCoordinates;found=true;break;}
     Release(ref output);
    }
    if(!found)Release(ref adapter);
   }
   Check(D3D11CreateDevice(adapter,0,IntPtr.Zero,0x20,IntPtr.Zero,0,7,out device,out _,out context),"D3D11CreateDevice");
   output1=Query(output,new Guid("00cddea8-939b-4b83-a340-a685226666cc"));
   Check(Call<Duplicate>(output1,22)(output1,device,out duplication),"DuplicateOutput");
   acquire=Call<Acquire>(duplication,8); releaseFrame=Call<ReleaseFrame>(duplication,14);
   copyRegion=Call<CopyRegion>(context,46); map=Call<Map>(context,14); unmap=Call<Unmap>(context,15);
  } finally { Release(ref output1); Release(ref output); Release(ref adapter); Release(ref factory); }
 }
 public byte[]? Capture(IntPtr hwnd,out int captureWidth,out int captureHeight)
 {
  if(!GetWindowRect(hwnd,out var rect)) throw new InvalidOperationException("Cannot obtain island bounds.");
  captureWidth=rect.Right-rect.Left;captureHeight=rect.Bottom-rect.Top;
  var currentDevice=new TokNotch.Infrastructure.Windows.MonitorService().AtPoint((rect.Left+rect.Right)/2,(rect.Top+rect.Bottom)/2).Device;
  if(currentDevice!=outputDevice)throw new COMException("Island moved to another output.",unchecked((int)0x887A0026));
  // During dragging, retain the last frame while the host straddles an output boundary.
  if(captureWidth<1||captureHeight<1||rect.Left<outputBounds.Left||rect.Top<outputBounds.Top||rect.Right>outputBounds.Right||rect.Bottom>outputBounds.Bottom){Thread.Sleep(30);return null;}
  IntPtr resource=IntPtr.Zero, texture=IntPtr.Zero; bool acquired=false,mapped=false;
  // Wait on the capture worker, never on the WPF render/UI thread. Static desktops do not busy-poll.
  int hr=acquire(duplication,100,info,out resource);
  if(hr==unchecked((int)0x887A0027)) return null;
  Check(hr,"AcquireNextFrame"); acquired=true;
  try {
   bool moved=rect.Left!=lastBounds.Left||rect.Top!=lastBounds.Top||rect.Right!=lastBounds.Right||rect.Bottom!=lastBounds.Bottom;
   // Dirty rectangles can omit changes behind WDA_EXCLUDEFROMCAPTURE windows.
   // Only skip pointer-only updates; compare the actual crop before publishing it.
   if(!moved&&previous.Length!=0&&Marshal.ReadInt64(info)==0) { SkippedFrames++; return null; }
   texture=Query(resource,new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c"));
   if(width!=captureWidth||height!=captureHeight) {
    Release(ref staging); width=captureWidth; height=captureHeight;
    var desc=new TextureDesc { Width=(uint)width, Height=(uint)height, MipLevels=1,ArraySize=1,Format=87,SampleCount=1,Usage=3,CpuAccessFlags=0x20000 };
    Check(Call<CreateTexture>(device,5)(device,ref desc,IntPtr.Zero,out staging)); pixels=new byte[checked(width*height*4)]; previous=new byte[pixels.Length]; TextureAllocations++;
   }
   var box=new Box { Left=(uint)(rect.Left-outputBounds.Left),Top=(uint)(rect.Top-outputBounds.Top),Right=(uint)(rect.Right-outputBounds.Left),Bottom=(uint)(rect.Bottom-outputBounds.Top),Back=1 };
   copyRegion(context,staging,0,0,0,0,texture,0,ref box);
   Check(map(context,staging,0,1,0,out var data),"Map"); mapped=true; Readbacks++;
   for(int row=0;row<height;row++) Marshal.Copy(IntPtr.Add(data.Data,checked(row*(int)data.RowPitch)),pixels,row*width*4,width*4);
   bool changed=moved||!pixels.AsSpan().SequenceEqual(previous);
   lastBounds=rect;
   if(!changed) { SkippedFrames++; return null; }
   pixels.AsSpan().CopyTo(previous);
   return pixels;
  } finally { if(mapped) unmap(context,staging,0); Release(ref texture); Release(ref resource); if(acquired) Check(releaseFrame(duplication)); }
 }
 public void Dispose() { Release(ref staging); Release(ref duplication); Release(ref context); Release(ref device); if(info!=IntPtr.Zero) { Marshal.FreeHGlobal(info); info=IntPtr.Zero; } pixels=previous=Array.Empty<byte>(); }
 private static void Check(int hr,string operation="COM") { if(hr<0) { var error=Marshal.GetExceptionForHR(hr)!; error.Data["NativeOperation"]=operation; throw error; } }
 private static IntPtr Query(IntPtr obj,Guid iid) { Check(Marshal.QueryInterface(obj,in iid,out var result)); return result; }
 private static void Release(ref IntPtr obj) { if(obj!=IntPtr.Zero) { Marshal.Release(obj); obj=IntPtr.Zero; } }
 private static T Call<T>(IntPtr obj,int slot) where T:Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size));
 [StructLayout(LayoutKind.Sequential)] private struct RectI { public int Left,Top,Right,Bottom; }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct OutputDesc { [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName; public RectI DesktopCoordinates; public int AttachedToDesktop,Rotation; public IntPtr Monitor; }
 [StructLayout(LayoutKind.Sequential)] private struct TextureDesc { public uint Width,Height,MipLevels,ArraySize,Format,SampleCount,SampleQuality,Usage,BindFlags,CpuAccessFlags,MiscFlags; }
 [StructLayout(LayoutKind.Sequential)] private struct Box { public uint Left,Top,Front,Right,Bottom,Back; }
 [StructLayout(LayoutKind.Sequential)] private struct Mapped { public IntPtr Data; public uint RowPitch,DepthPitch; }
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetObject(IntPtr self,out IntPtr result);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumOutput(IntPtr self,uint index,out IntPtr output);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetOutputDesc(IntPtr self,out OutputDesc desc);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Duplicate(IntPtr self,IntPtr device,out IntPtr result);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Acquire(IntPtr self,uint timeout,IntPtr frameInfo,out IntPtr resource);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReleaseFrame(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateTexture(IntPtr self,ref TextureDesc desc,IntPtr initial,out IntPtr texture);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void CopyRegion(IntPtr self,IntPtr destination,uint subresource,uint x,uint y,uint z,IntPtr source,uint sourceSubresource,ref Box box);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Map(IntPtr self,IntPtr resource,uint subresource,uint type,uint flags,out Mapped mapped);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void Unmap(IntPtr self,IntPtr resource,uint subresource);
 [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(IntPtr adapter,uint driver,IntPtr software,uint flags,IntPtr levels,uint count,uint version,out IntPtr device,out uint feature,out IntPtr context);
 [DllImport("dxgi.dll")] private static extern int CreateDXGIFactory1(in Guid iid,out IntPtr factory);
 [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out RectI rect);
 [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
}

