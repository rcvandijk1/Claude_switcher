using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClaudeSwitcher.Core;
using WpfApp = System.Windows.Application;

namespace ClaudeSwitcher.App;

public sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ProfileRepository _repo;
    private readonly Func<Guid, Task> _switchHandler;
    private readonly Action _openWindow;

    public TrayController(ProfileRepository repo, Func<Guid, Task> switchHandler, Action openWindow)
    {
        _repo = repo;
        _switchHandler = switchHandler;
        _openWindow = openWindow;

        _icon = new NotifyIcon
        {
            Visible = true,
            Text = "Claude Switcher",
            Icon = BuildIcon("#D97706")
        };

        _icon.DoubleClick += (_, _) => _openWindow();
        Refresh();
    }

    public void Refresh()
    {
        var menu = new ContextMenuStrip();

        var store = _repo.Load();
        var activeId = store.ActiveProfileId;

        if (store.Profiles.Count == 0)
        {
            menu.Items.Add("(no profiles)").Enabled = false;
        }
        else
        {
            foreach (var p in store.Profiles)
            {
                var label = activeId == p.Id ? $"✓ {p.Name} (active)" : $"Switch to {p.Name}";
                var item = new ToolStripMenuItem(label);
                if (activeId == p.Id) item.Enabled = false;
                var id = p.Id;
                item.Click += async (_, _) => await _switchHandler(id);
                menu.Items.Add(item);
            }
            try { _icon.Icon = BuildIcon(store.Profiles.FirstOrDefault(p => p.Id == activeId)?.ColorHex ?? "#D97706"); }
            catch { }
        }

        menu.Items.Add(new ToolStripSeparator());
        var open = new ToolStripMenuItem("Open Switcher…");
        open.Click += (_, _) => _openWindow();
        menu.Items.Add(open);

        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => WpfApp.Current.Shutdown();
        menu.Items.Add(quit);

        _icon.ContextMenuStrip = menu;

        var active = store.Profiles.FirstOrDefault(p => p.Id == activeId);
        _icon.Text = active is null ? "Claude Switcher" : $"Claude Switcher — {active.Name}";
    }

    public void Notify(string title, string body, ToolTipIcon iconType = ToolTipIcon.Info)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = body;
        _icon.BalloonTipIcon = iconType;
        _icon.ShowBalloonTip(4000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private static Icon BuildIcon(string colorHex)
    {
        var color = ColorTranslator.FromHtml(colorHex);
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 2, 2, 28, 28);
            using var pen = new Pen(Color.White, 2);
            g.DrawEllipse(pen, 2, 2, 28, 28);
        }
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
