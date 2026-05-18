namespace ClaudeSwitcher.Core;

internal static class FsUtil
{
    /// <summary>
    /// Recursively copy <paramref name="src"/> into <paramref name="dst"/>.
    /// Files matching <paramref name="excludePatterns"/> (case-insensitive contains) are skipped.
    /// Files locked by another process are skipped with a logged warning rather than aborting.
    /// </summary>
    public static int CopyDirectory(string src, string dst, IEnumerable<string>? excludePatterns = null, IList<string>? warnings = null)
    {
        if (!Directory.Exists(src)) return 0;
        Directory.CreateDirectory(dst);
        var excludes = excludePatterns?.ToArray() ?? Array.Empty<string>();
        var copied = 0;

        foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, dir);
            if (IsExcluded(rel, excludes)) continue;
            Directory.CreateDirectory(Path.Combine(dst, rel));
        }

        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            if (IsExcluded(rel, excludes)) continue;
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            try
            {
                File.Copy(file, target, overwrite: true);
                copied++;
            }
            catch (IOException ex)
            {
                warnings?.Add($"skip {rel}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                warnings?.Add($"skip {rel}: {ex.Message}");
            }
        }
        return copied;
    }

    public static void EmptyDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir)) File.Delete(f);
        foreach (var d in Directory.EnumerateDirectories(dir)) Directory.Delete(d, recursive: true);
    }

    private static bool IsExcluded(string relPath, string[] excludes)
    {
        foreach (var e in excludes)
            if (relPath.Contains(e, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
