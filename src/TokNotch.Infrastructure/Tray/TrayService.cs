using System.Drawing;
using System.Windows.Forms;
namespace TokNotch.Infrastructure.Tray;

/// <summary>
/// Tray icon with quick actions only. Expansion mode and material moved into the settings window
/// (they are saved preferences now), so this menu no longer carries state that must stay in sync.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _ownedIcon;
    private readonly ContextMenuStrip _menu;
    public event Action? ShowRequested;
    public event Action? ExitRequested; public event Action? SettingsRequested; public event Action? RefreshRequested;
    public TrayService()
    {
        _ownedIcon = (Icon)SystemIcons.Information.Clone();
        _menu = new ContextMenuStrip();
        _menu.Items.Add("打开浮岛", null, (_, _) => ShowRequested?.Invoke());
        _menu.Items.Add("立即刷新数据", null, (_, _) => RefreshRequested?.Invoke());
        _menu.Items.Add("设置…", null, (_, _) => SettingsRequested?.Invoke());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke());
        _icon = new NotifyIcon { Icon = _ownedIcon, Text = "TokNotch · AI 用量", ContextMenuStrip = _menu, Visible = true };
        _icon.DoubleClick += (_, _) => ShowRequested?.Invoke();
    }
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _ownedIcon.Dispose(); }
}
