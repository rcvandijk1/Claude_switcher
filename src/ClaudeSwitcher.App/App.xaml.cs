using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ClaudeSwitcher.App.Ipc;
using ClaudeSwitcher.App.Migration;
using ClaudeSwitcher.Core;
using ClaudeSwitcher.Core.Providers;

namespace ClaudeSwitcher.App;

public partial class App : System.Windows.Application
{
    private static readonly System.Threading.Mutex SingleInstance =
        new(initiallyOwned: true, name: "Local\\ClaudeSwitcherSingleInstance", out _singleInstanceCreated);
    private static readonly bool _singleInstanceCreated;

    public ProfileRepository Repo { get; private set; } = null!;
    public SwitchOrchestrator Orchestrator { get; private set; } = null!;
    public PipeServer Pipe { get; private set; } = null!;
    public IChromeBridge ChromeBridge { get; private set; } = null!;
    public TrayController Tray { get; private set; } = null!;

    public new static App Current => (App)System.Windows.Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!_singleInstanceCreated)
        {
            MessageBox.Show("Claude Switcher is already running. Look for its icon in the system tray.",
                "Claude Switcher", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Repo = new ProfileRepository();
        try { LegacyMigrator.Run(Repo); }
        catch (Exception ex)
        {
            MessageBox.Show($"Legacy migration failed (non-fatal): {ex.Message}",
                "Claude Switcher", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        Pipe = new PipeServer();
        Pipe.Start();
        var chromeBridge = new PipeChromeBridge(Pipe);
        ChromeBridge = chromeBridge;

        var providers = new ISwapProvider[]
        {
            new ClaudeCliProvider(),
            new ClaudeDesktopProvider(),
            new AntigravityProvider(),
            new ChromeProvider(chromeBridge),
        };
        Orchestrator = new SwitchOrchestrator(Repo, providers);

        Tray = new TrayController(Repo, OnSwitchRequested, OpenMainWindow);
        Pipe.Connected += () => Dispatcher.Invoke(() => Tray.Notify("Chrome connected", "Browser cookie switching is active."));
        Pipe.Disconnected += () => Dispatcher.Invoke(() => { /* keep silent */ });

        OpenMainWindow();
    }

    public void OpenMainWindow()
    {
        var existing = Windows.OfType<MainWindow>().FirstOrDefault();
        if (existing != null)
        {
            existing.Show();
            existing.Activate();
            existing.WindowState = WindowState.Normal;
            return;
        }
        var w = new MainWindow();
        w.Show();
    }

    public async Task OnSwitchRequested(Guid id)
    {
        try
        {
            var results = await Orchestrator.SwitchAsync(id, progress: null, CancellationToken.None);
            Tray.Refresh();
            var failures = results.Count(r => r.Status == SwapStatus.Failed);
            var ok = results.Count(r => r.Status == SwapStatus.Ok);
            var skipped = results.Count(r => r.Status == SwapStatus.Skipped);
            Tray.Notify(
                failures == 0 ? "Profile switched" : "Switched with errors",
                $"{ok} applied, {skipped} skipped, {failures} failed",
                failures == 0 ? System.Windows.Forms.ToolTipIcon.Info : System.Windows.Forms.ToolTipIcon.Warning);
            foreach (var w in Windows.OfType<MainWindow>())
                w.RefreshList();
        }
        catch (Exception ex)
        {
            Tray.Notify("Switch failed", ex.Message, System.Windows.Forms.ToolTipIcon.Error);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try { await Pipe.DisposeAsync(); } catch { }
        Tray?.Dispose();
        base.OnExit(e);
    }
}
