using System;
using System.IO;
using System.Text;
using System.Text.Json;
using ClaudeSwitcher.Core;

namespace ClaudeSwitcher.App.Migration;

/// <summary>
/// One-shot import of the legacy Python switcher's
/// <c>~/.claude_switcher/profiles.json</c> into the new on-disk layout.
/// Each old "account" becomes a <see cref="Profile"/>, and its embedded
/// <c>claudeAiOauth</c> credentials are written into the per-profile
/// <c>claude-cli/credentials.json.dpapi</c> snapshot file.
/// </summary>
public static class LegacyMigrator
{
    public static int Run(ProfileRepository repo)
    {
        if (!File.Exists(Paths.ProfilesIndex)) return 0;

        string raw;
        try { raw = File.ReadAllText(Paths.ProfilesIndex); }
        catch (IOException) { return 0; }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(raw); }
        catch (JsonException) { return 0; }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("accounts", out var accounts)) return 0;
            if (root.TryGetProperty("profiles", out _)) return 0; // already migrated

            // Back the legacy file up before we overwrite it.
            var backup = Paths.ProfilesIndex + ".legacy-backup";
            if (!File.Exists(backup)) File.Copy(Paths.ProfilesIndex, backup);

            var store = new ProfileStore();
            string? activeIdString = root.TryGetProperty("active_id", out var aid) && aid.ValueKind == JsonValueKind.String
                ? aid.GetString() : null;

            var imported = 0;
            foreach (var acc in accounts.EnumerateArray())
            {
                if (!acc.TryGetProperty("name", out var nameEl)) continue;
                var name = nameEl.GetString() ?? "Imported";

                var profile = new Profile { Name = name };
                store.Profiles.Add(profile);

                if (activeIdString != null
                    && acc.TryGetProperty("id", out var oldIdEl)
                    && oldIdEl.GetString() == activeIdString)
                {
                    store.ActiveProfileId = profile.Id;
                }

                if (acc.TryGetProperty("credentials", out var creds) && creds.ValueKind == JsonValueKind.Object)
                {
                    var snapshot = Path.Combine(Paths.ProfileDir(profile.Id), "claude-cli", "credentials.json.dpapi");
                    var bytes = Encoding.UTF8.GetBytes(creds.GetRawText());
                    DpapiVault.WriteProtectedFile(snapshot, bytes);
                }

                imported++;
            }

            repo.Save(store);
            return imported;
        }
    }
}
