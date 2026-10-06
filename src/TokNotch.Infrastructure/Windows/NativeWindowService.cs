using System.ComponentModel;
using System.Runtime.InteropServices;
namespace TokNotch.Infrastructure.Windows;

public sealed class NativeWindowService(nint handle)
{
    public nint Handle { get; } = handle;
    public void Configure()
    {
        var style = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GwlExStyle,
            style | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow);
        var applied = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GwlExStyle);
        if ((applied & NativeMethods.WsExNoActivate) == 0) throw new Win32Exception("NOACTIVATE style was not applied.");
    }
    public void SetBounds(int x, int y, int width, int height)
    {
        if (!NativeMethods.SetWindowPos(Handle, new nint(-1), x, y, Math.Max(1, width), Math.Max(1, height),
                NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
