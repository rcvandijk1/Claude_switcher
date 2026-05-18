namespace ClaudeSwitcher.Core.Providers;

public sealed class AntigravityProvider : ElectronAppProvider
{
    public override SwapTarget Target => SwapTarget.Antigravity;
    public override string DisplayName => "Antigravity";

    protected override string DataDir => Paths.AntigravityData;
    protected override string SnapshotSubdir => "antigravity";
    protected override IReadOnlyList<string> ProcessNames { get; } = new[] { "Antigravity" };

    protected override IReadOnlyList<string> ExeFallbacks { get; } = new[] { Paths.AntigravityExe };
}
