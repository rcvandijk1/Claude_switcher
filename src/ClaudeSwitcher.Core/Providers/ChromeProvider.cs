using System.Text;

namespace ClaudeSwitcher.Core.Providers;

public sealed class ChromeProvider : ISwapProvider
{
    private readonly IChromeBridge _bridge;
    private const string SnapshotFile = "chrome-cookies.json.dpapi";

    public ChromeProvider(IChromeBridge bridge) => _bridge = bridge;

    public SwapTarget Target => SwapTarget.Chrome;
    public string DisplayName => "Chrome (claude.ai tabs)";

    public Task<bool> IsInstalledAsync(CancellationToken ct) =>
        Task.FromResult(_bridge.IsConnected);

    public async Task<SwapResult> CaptureAsync(string profileDir, CancellationToken ct)
    {
        if (!_bridge.IsConnected)
            return new SwapResult(Target, SwapStatus.Skipped, "browser extension not connected");

        try
        {
            var json = await _bridge.CaptureCookiesJsonAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return new SwapResult(Target, SwapStatus.Skipped, "no claude.ai session in browser");

            var path = Path.Combine(profileDir, "chrome", SnapshotFile);
            DpapiVault.WriteProtectedFile(path, Encoding.UTF8.GetBytes(json));
            return new SwapResult(Target, SwapStatus.Ok, "browser cookies captured");
        }
        catch (Exception ex)
        {
            return new SwapResult(Target, SwapStatus.Failed, ex.Message);
        }
    }

    public async Task<SwapResult> ApplyAsync(string profileDir, bool relaunchApps, CancellationToken ct)
    {
        if (!_bridge.IsConnected)
            return new SwapResult(Target, SwapStatus.Failed, "browser extension not connected");

        var path = Path.Combine(profileDir, "chrome", SnapshotFile);
        if (!File.Exists(path))
            return new SwapResult(Target, SwapStatus.Failed, "no browser cookies saved for this profile");

        try
        {
            var bytes = DpapiVault.ReadProtectedFile(path);
            var json = Encoding.UTF8.GetString(bytes);
            await _bridge.ApplyCookiesJsonAsync(json, ct);
            return new SwapResult(Target, SwapStatus.Ok, "browser cookies applied, tabs reloaded");
        }
        catch (Exception ex)
        {
            return new SwapResult(Target, SwapStatus.Failed, ex.Message);
        }
    }
}
