namespace ClaudeSwitcher.Core;

public interface ISwapProvider
{
    SwapTarget Target { get; }
    string DisplayName { get; }

    Task<bool> IsInstalledAsync(CancellationToken ct);

    /// <summary>Read live system state and persist it into the profile dir.</summary>
    Task<SwapResult> CaptureAsync(string profileDir, CancellationToken ct);

    /// <summary>Apply the snapshot from the profile dir onto the live system.</summary>
    Task<SwapResult> ApplyAsync(string profileDir, bool relaunchApps, CancellationToken ct);
}
