namespace ClaudeSwitcher.Core;

public static class Paths
{
    public static string UserHome { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string AppData { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static string LocalAppData { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string SwitcherRoot { get; } =
        Path.Combine(UserHome, ".claude_switcher");

    public static string ProfilesIndex { get; } =
        Path.Combine(SwitcherRoot, "profiles.json");

    public static string ProfilesDir { get; } =
        Path.Combine(SwitcherRoot, "profiles");

    public static string LogsDir { get; } =
        Path.Combine(SwitcherRoot, "logs");

    public static string ClaudeCliDir { get; } = Path.Combine(UserHome, ".claude");
    public static string ClaudeCliCredentials { get; } =
        Path.Combine(ClaudeCliDir, ".credentials.json");

    public static string ClaudeDesktopData { get; } = Path.Combine(AppData, "Claude");
    public static string AntigravityData { get; } = Path.Combine(AppData, "Antigravity");

    public static string AntigravityExe { get; } =
        Path.Combine(LocalAppData, "Programs", "Antigravity", "Antigravity.exe");

    public static string ProfileDir(Guid id) =>
        Path.Combine(ProfilesDir, id.ToString("D"));
}
