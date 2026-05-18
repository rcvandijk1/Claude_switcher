using System.Security.Cryptography;
using System.Text;

namespace ClaudeSwitcher.Core;

public static class DpapiVault
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ClaudeSwitcher/v1");

    public static byte[] Protect(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

    public static byte[] Unprotect(byte[] ciphertext) =>
        ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);

    public static void WriteProtectedFile(string path, byte[] plaintext)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, Protect(plaintext));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    public static byte[] ReadProtectedFile(string path) =>
        Unprotect(File.ReadAllBytes(path));
}
