namespace ClaudeSwitcher.Core;

public sealed class SwitchOrchestrator
{
    private readonly ProfileRepository _repo;
    private readonly IReadOnlyList<ISwapProvider> _providers;

    public SwitchOrchestrator(ProfileRepository repo, IEnumerable<ISwapProvider> providers)
    {
        _repo = repo;
        _providers = providers.ToList();
    }

    public IReadOnlyList<ISwapProvider> Providers => _providers;

    /// <summary>
    /// Capture the current live state into <paramref name="profileId"/>.
    /// Used by "Add account → capture from current login".
    /// </summary>
    public async Task<IReadOnlyList<SwapResult>> CaptureAsync(
        Guid profileId,
        IProgress<SwapResult>? progress,
        CancellationToken ct)
    {
        var store = _repo.Load();
        var profile = store.Profiles.FirstOrDefault(p => p.Id == profileId)
            ?? throw new InvalidOperationException($"Unknown profile {profileId}");
        var dir = Paths.ProfileDir(profileId);
        Directory.CreateDirectory(dir);
        var results = new List<SwapResult>();
        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();
            SwapResult result;
            if (!profile.EnabledTargets.Contains(provider.Target))
            {
                result = new SwapResult(provider.Target, SwapStatus.Skipped, "disabled for this profile");
            }
            else if (!await provider.IsInstalledAsync(ct))
            {
                result = new SwapResult(provider.Target, SwapStatus.Skipped, "not installed / unavailable");
            }
            else
            {
                try
                {
                    result = await provider.CaptureAsync(dir, ct);
                }
                catch (Exception ex)
                {
                    result = new SwapResult(provider.Target, SwapStatus.Failed, ex.Message);
                }
            }
            results.Add(result);
            progress?.Report(result);
        }
        return results;
    }

    /// <summary>
    /// Switch to <paramref name="targetId"/>. If a different profile is currently
    /// active, its live state is captured first so logins done since last switch
    /// are preserved.
    /// </summary>
    public async Task<IReadOnlyList<SwapResult>> SwitchAsync(
        Guid targetId,
        IProgress<SwapResult>? progress,
        CancellationToken ct)
    {
        var store = _repo.Load();
        var target = store.Profiles.FirstOrDefault(p => p.Id == targetId)
            ?? throw new InvalidOperationException($"Unknown profile {targetId}");

        // 1. Snapshot the currently-active profile's live state, if any.
        if (store.ActiveProfileId is Guid activeId && activeId != targetId)
        {
            await CaptureAsync(activeId, progress, ct);
        }

        // 2. Apply target profile to live for each enabled+installed provider.
        var results = new List<SwapResult>();
        var targetDir = Paths.ProfileDir(targetId);
        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();
            SwapResult result;
            if (!target.EnabledTargets.Contains(provider.Target))
            {
                result = new SwapResult(provider.Target, SwapStatus.Skipped, "disabled for this profile");
            }
            else if (!await provider.IsInstalledAsync(ct))
            {
                result = new SwapResult(provider.Target, SwapStatus.Skipped, "not installed / unavailable");
            }
            else
            {
                try
                {
                    result = await provider.ApplyAsync(targetDir, store.RelaunchApps, ct);
                }
                catch (Exception ex)
                {
                    result = new SwapResult(provider.Target, SwapStatus.Failed, ex.Message);
                }
            }
            results.Add(result);
            progress?.Report(result);
        }

        target.LastActivated = DateTimeOffset.UtcNow;
        store.ActiveProfileId = targetId;
        _repo.Save(store);

        return results;
    }
}
