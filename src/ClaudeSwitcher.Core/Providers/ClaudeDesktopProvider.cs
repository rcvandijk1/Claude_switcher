using System.Diagnostics;

namespace ClaudeSwitcher.Core.Providers;

/// <summary>
/// Anthropic's Claude Desktop on Windows is delivered as an MSIX/UWP package
/// (AUMID <c>Claude_pzs8sxrjxfjjc!Claude</c>). The auth-bearing data lives at
/// the unvirtualized path <c>%APPDATA%\Claude</c>, so file swap works as for
/// any Electron app, but relaunch must go through shell activation.
/// </summary>
public sealed class ClaudeDesktopProvider : ElectronAppProvider
{
    private const string Aumid = "Claude_pzs8sxrjxfjjc!Claude";

    public override SwapTarget Target => SwapTarget.ClaudeDesktop;
    public override string DisplayName => "Claude Desktop";

    protected override string DataDir => Paths.ClaudeDesktopData;
    protected override string SnapshotSubdir => "claude-desktop";
    protected override IReadOnlyList<string> ProcessNames { get; } = new[] { "Claude" };

    protected override ProcessStartInfo BuildRelaunchStartInfo(string? capturedExe) =>
        new()
        {
            FileName = "explorer.exe",
            Arguments = $"shell:AppsFolder\\{Aumid}",
            UseShellExecute = false
        };
}
