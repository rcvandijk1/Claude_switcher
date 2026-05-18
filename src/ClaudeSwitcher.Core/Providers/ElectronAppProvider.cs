using System.Diagnostics;

namespace ClaudeSwitcher.Core.Providers;

/// <summary>
/// Base provider for Chromium/Electron-based apps (Claude Desktop, Antigravity)
/// whose auth lives in their per-user data dir under %APPDATA%.
///
/// Snapshot strategy: the file <c>Local State</c> holds the DPAPI-wrapped
/// encryption key used to encrypt cookies inside <c>Network\Cookies</c>. Both
/// MUST be swapped as one unit, otherwise the cookies are unreadable. We also
/// snapshot Local Storage and Session Storage where Electron apps frequently
/// stash tokens / refresh tokens.
/// </summary>
public abstract class ElectronAppProvider : ISwapProvider
{
    public abstract SwapTarget Target { get; }
    public abstract string DisplayName { get; }

    protected abstract string DataDir { get; }
    protected abstract string SnapshotSubdir { get; }

    /// <summary>Process image names to look for and terminate (without ".exe").</summary>
    protected abstract IReadOnlyList<string> ProcessNames { get; }

    /// <summary>Fallback install paths if no live process discloses the exe.</summary>
    protected virtual IReadOnlyList<string> ExeFallbacks { get; } = Array.Empty<string>();

    /// <summary>
    /// Build a <see cref="ProcessStartInfo"/> for relaunching after a swap.
    /// Default implementation uses the previously-discovered exe path (or
    /// an entry from <see cref="ExeFallbacks"/>). Override for MSIX apps that
    /// must be activated via <c>shell:AppsFolder\&lt;aumid&gt;</c>.
    /// </summary>
    protected virtual ProcessStartInfo? BuildRelaunchStartInfo(string? capturedExe)
    {
        var exe = (capturedExe != null && File.Exists(capturedExe)) ? capturedExe : FirstExistingFallback();
        if (exe == null) return null;
        return new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe)
        };
    }

    /// <summary>
    /// Relative paths (from DataDir) to snapshot. Both files and directories OK.
    /// </summary>
    protected virtual IReadOnlyList<string> SnapshotItems { get; } = new[]
    {
        "Local State",
        "Preferences",
        Path.Combine("Network", "Cookies"),
        Path.Combine("Network", "Cookies-journal"),
        Path.Combine("Network", "Network Persistent State"),
        Path.Combine("Network", "TransportSecurity"),
        Path.Combine("Network", "Trust Tokens"),
        Path.Combine("Network", "Trust Tokens-journal"),
        "Local Storage",
        "Session Storage",
        "IndexedDB",
        "WebStorage",
        "SharedStorage"
    };

    public Task<bool> IsInstalledAsync(CancellationToken ct) =>
        Task.FromResult(Directory.Exists(DataDir));

    public async Task<SwapResult> CaptureAsync(string profileDir, CancellationToken ct)
    {
        var (exe, wasRunning) = StopApp(ct);
        var snapshotRoot = Path.Combine(profileDir, SnapshotSubdir);
        FsUtil.EmptyDirectory(snapshotRoot);
        var warnings = new List<string>();
        var copied = CopyItems(DataDir, snapshotRoot, warnings);
        if (wasRunning) TryRelaunch(BuildRelaunchStartInfo(exe));

        var msg = copied == 0
            ? "no auth files found"
            : $"captured {copied} files" + (warnings.Count > 0 ? $" ({warnings.Count} warnings)" : "");
        await Task.Yield();
        return new SwapResult(Target, copied == 0 ? SwapStatus.Failed : SwapStatus.Ok, msg);
    }

    public async Task<SwapResult> ApplyAsync(string profileDir, bool relaunchApps, CancellationToken ct)
    {
        var snapshotRoot = Path.Combine(profileDir, SnapshotSubdir);
        if (!Directory.Exists(snapshotRoot))
            return new SwapResult(Target, SwapStatus.Failed, "no snapshot saved for this profile");

        var (exe, wasRunning) = StopApp(ct);
        // Remove the live versions of the items we're about to lay down, so that
        // stale state from the previous account doesn't leak in (e.g., an old
        // leveldb log file that no longer has a matching ldb).
        foreach (var rel in SnapshotItems)
        {
            var live = Path.Combine(DataDir, rel);
            try
            {
                if (File.Exists(live)) File.Delete(live);
                else if (Directory.Exists(live)) Directory.Delete(live, recursive: true);
            }
            catch (IOException) { /* may be locked by something else; carry on */ }
        }

        var warnings = new List<string>();
        var copied = FsUtil.CopyDirectory(snapshotRoot, DataDir, warnings: warnings);

        if (relaunchApps && wasRunning)
        {
            TryRelaunch(BuildRelaunchStartInfo(exe));
        }

        await Task.Yield();
        var msg = $"applied {copied} files" + (warnings.Count > 0 ? $" ({warnings.Count} warnings)" : "");
        return new SwapResult(Target, SwapStatus.Ok, msg);
    }

    // ---------------------------------------------------------------- helpers

    private int CopyItems(string srcRoot, string dstRoot, IList<string> warnings)
    {
        var total = 0;
        foreach (var rel in SnapshotItems)
        {
            var src = Path.Combine(srcRoot, rel);
            var dst = Path.Combine(dstRoot, rel);
            if (File.Exists(src))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                try { File.Copy(src, dst, overwrite: true); total++; }
                catch (IOException ex) { warnings.Add($"skip {rel}: {ex.Message}"); }
                catch (UnauthorizedAccessException ex) { warnings.Add($"skip {rel}: {ex.Message}"); }
            }
            else if (Directory.Exists(src))
            {
                total += FsUtil.CopyDirectory(src, dst, warnings: warnings);
            }
        }
        return total;
    }

    private (string? Exe, bool WasRunning) StopApp(CancellationToken ct)
    {
        string? exe = null;
        var ran = false;
        foreach (var name in ProcessNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                ran = true;
                try
                {
                    exe ??= p.MainModule?.FileName;
                }
                catch { /* access denied for some processes */ }

                try
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(2000))
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(5000);
                    }
                }
                catch { }
                finally { p.Dispose(); }
                ct.ThrowIfCancellationRequested();
            }
        }
        // Brief settle so file handles release.
        if (ran) Thread.Sleep(300);
        return (exe, ran);
    }

    private void TryRelaunch(ProcessStartInfo? psi)
    {
        if (psi == null) return;
        try { Process.Start(psi); }
        catch { /* user can launch it themselves */ }
    }

    private string? FirstExistingFallback()
    {
        foreach (var p in ExeFallbacks)
            if (File.Exists(p)) return p;
        return null;
    }
}
