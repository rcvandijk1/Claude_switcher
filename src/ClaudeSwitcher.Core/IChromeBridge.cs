namespace ClaudeSwitcher.Core;

/// <summary>
/// Abstraction over the Chrome MV3 extension connection (over native messaging /
/// named pipe). The provider asks the bridge to capture or apply claude.ai cookies;
/// the bridge round-trips a JSON request to the extension's service worker.
/// </summary>
public interface IChromeBridge
{
    bool IsConnected { get; }
    Task<string> CaptureCookiesJsonAsync(CancellationToken ct);
    Task ApplyCookiesJsonAsync(string cookiesJson, CancellationToken ct);
}
