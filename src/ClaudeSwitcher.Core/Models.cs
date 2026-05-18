namespace ClaudeSwitcher.Core;

public enum SwapTarget
{
    ClaudeCli,
    ClaudeDesktop,
    Antigravity,
    Chrome
}

public enum SwapStatus
{
    Ok,
    Skipped,
    Failed
}

public sealed record SwapResult(
    SwapTarget Target,
    SwapStatus Status,
    string? Message);

public sealed class Profile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#D97706";
    public DateTimeOffset Added { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastActivated { get; set; }
    public HashSet<SwapTarget> EnabledTargets { get; set; } = new()
    {
        SwapTarget.ClaudeCli,
        SwapTarget.ClaudeDesktop,
        SwapTarget.Antigravity,
        SwapTarget.Chrome
    };
}

public sealed class ProfileStore
{
    public int SchemaVersion { get; set; } = 1;
    public List<Profile> Profiles { get; set; } = new();
    public Guid? ActiveProfileId { get; set; }
    public bool RelaunchApps { get; set; } = true;
}
