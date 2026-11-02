using Forms = System.Windows.Forms;

namespace FloatingLyrics.Platform.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _toggle;
    private readonly Forms.ToolStripMenuItem _lock;
    public TrayIcon(Action settings, Action toggle, Action toggleLock, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        _toggle = new Forms.ToolStripMenuItem("Hide lyrics", null, (_, _) => toggle());
        _lock = new Forms.ToolStripMenuItem("Unlock overlay", null, (_, _) => toggleLock());
        menu.Items.Add(_toggle);
        menu.Items.Add(_lock);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => settings());
        menu.Items.Add("Exit", null, (_, _) => exit());
        _icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Floating Lyrics · No media detected",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => settings();
    }
    public void Update(bool enabled, bool locked, string status)
    {
        _toggle.Text = enabled ? "Hide lyrics" : "Show lyrics";
        _lock.Text = locked ? "Unlock overlay" : "Lock overlay";
        var label = "Floating Lyrics · " + status;
        _icon.Text = label[..Math.Min(63, label.Length)];
    }
    public void Dispose() { _icon.Visible = false; _icon.ContextMenuStrip?.Dispose(); _icon.Dispose(); }
}
