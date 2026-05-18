namespace ClaudeSwitcher.Core.Providers;

public sealed class ClaudeCliProvider : ISwapProvider
{
    private const string SnapshotFileName = "credentials.json.dpapi";

    public SwapTarget Target => SwapTarget.ClaudeCli;
    public string DisplayName => "Claude Code CLI";

    public Task<bool> IsInstalledAsync(CancellationToken ct) =>
        Task.FromResult(Directory.Exists(Paths.ClaudeCliDir));

    public Task<SwapResult> CaptureAsync(string profileDir, CancellationToken ct)
    {
        if (!File.Exists(Paths.ClaudeCliCredentials))
            return Task.FromResult(new SwapResult(Target, SwapStatus.Skipped, "no live credentials to capture"));

        var snapshot = Path.Combine(profileDir, "claude-cli", SnapshotFileName);
        var bytes = File.ReadAllBytes(Paths.ClaudeCliCredentials);
        DpapiVault.WriteProtectedFile(snapshot, bytes);
        return Task.FromResult(new SwapResult(Target, SwapStatus.Ok, "credentials captured"));
    }

    public Task<SwapResult> ApplyAsync(string profileDir, bool relaunchApps, CancellationToken ct)
    {
        var snapshot = Path.Combine(profileDir, "claude-cli", SnapshotFileName);
        if (!File.Exists(snapshot))
            return Task.FromResult(new SwapResult(Target, SwapStatus.Failed, "no snapshot saved for this profile"));

        Directory.CreateDirectory(Paths.ClaudeCliDir);
        var plaintext = DpapiVault.ReadProtectedFile(snapshot);
        var tmp = Paths.ClaudeCliCredentials + ".tmp";
        File.WriteAllBytes(tmp, plaintext);
        if (File.Exists(Paths.ClaudeCliCredentials))
            File.Replace(tmp, Paths.ClaudeCliCredentials, null);
        else
            File.Move(tmp, Paths.ClaudeCliCredentials);

        return Task.FromResult(new SwapResult(Target, SwapStatus.Ok, "credentials applied"));
    }
}
