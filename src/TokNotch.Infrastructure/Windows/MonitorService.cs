using TokNotch.Core.Models;
namespace TokNotch.Infrastructure.Windows;

public sealed record MonitorWorkArea(int Left, int Top, int Right, int Bottom, string Device);
public sealed class MonitorService
{
    public IReadOnlyList<MonitorWorkArea> Displays=>System.Windows.Forms.Screen.AllScreens.Select(screen=>new MonitorWorkArea(screen.WorkingArea.Left,screen.WorkingArea.Top,screen.WorkingArea.Right,screen.WorkingArea.Bottom,screen.DeviceName)).ToArray();
    public MonitorWorkArea AtPoint(int x,int y)
    {
        var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x,y));
        return new(screen.WorkingArea.Left,screen.WorkingArea.Top,screen.WorkingArea.Right,screen.WorkingArea.Bottom,screen.DeviceName);
    }
    public MonitorWorkArea Resolve(WindowPreferences preferences)
    {
        if(preferences.Display==DisplayTarget.FollowCursor&&NativeMethods.GetCursorPos(out var point))return AtPoint(point.X,point.Y);
        if(preferences.Display==DisplayTarget.Specific&&Displays.FirstOrDefault(area=>area.Device==preferences.MonitorDevice) is {} specific)return specific;
        return PrimaryWorkArea();
    }
    public MonitorWorkArea PrimaryWorkArea()
    {
        var screen=System.Windows.Forms.Screen.PrimaryScreen??System.Windows.Forms.Screen.AllScreens[0];
        return new(screen.WorkingArea.Left,screen.WorkingArea.Top,screen.WorkingArea.Right,screen.WorkingArea.Bottom,screen.DeviceName);
    }
    public int DisplayCount => System.Windows.Forms.Screen.AllScreens.Length;
}
