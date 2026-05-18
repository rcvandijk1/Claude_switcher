using System.Text.Json;

namespace ClaudeSwitcher.Core;

public sealed class ProfileRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _lock = new();

    public ProfileStore Load()
    {
        Directory.CreateDirectory(Paths.SwitcherRoot);
        Directory.CreateDirectory(Paths.ProfilesDir);

        if (!File.Exists(Paths.ProfilesIndex))
            return new ProfileStore();

        try
        {
            var json = File.ReadAllText(Paths.ProfilesIndex);
            return JsonSerializer.Deserialize<ProfileStore>(json, JsonOpts) ?? new ProfileStore();
        }
        catch (JsonException)
        {
            var backup = Paths.ProfilesIndex + ".corrupt." + DateTime.UtcNow.Ticks;
            File.Copy(Paths.ProfilesIndex, backup, overwrite: false);
            return new ProfileStore();
        }
    }

    public void Save(ProfileStore store)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Paths.SwitcherRoot);
            var json = JsonSerializer.Serialize(store, JsonOpts);
            var tmp = Paths.ProfilesIndex + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(Paths.ProfilesIndex)) File.Replace(tmp, Paths.ProfilesIndex, null);
            else File.Move(tmp, Paths.ProfilesIndex);
        }
    }

    public Profile Add(ProfileStore store, string name, string colorHex)
    {
        var p = new Profile { Name = name, ColorHex = colorHex };
        store.Profiles.Add(p);
        Directory.CreateDirectory(Paths.ProfileDir(p.Id));
        Save(store);
        return p;
    }

    public void Delete(ProfileStore store, Guid id)
    {
        store.Profiles.RemoveAll(p => p.Id == id);
        if (store.ActiveProfileId == id) store.ActiveProfileId = null;
        var dir = Paths.ProfileDir(id);
        if (Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { /* leave behind; user can clean up */ }
        }
        Save(store);
    }
}
