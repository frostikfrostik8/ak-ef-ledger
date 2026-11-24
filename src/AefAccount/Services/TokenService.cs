using System.IO;
using System.Text;

namespace AefAccount.Services;

/// <summary>
/// Ключ Skport хранится только в <c>.secrets/</c>. Приложение не отправляет его
/// в сеть: файл читает <c>scripts/snapshot.py</c> и передаёт напрямую Skport API.
/// </summary>
public static class TokenService
{
    // Порядок совпадает с scripts/sync.py: TOKEN_CANDIDATES.
    private static readonly string[] FileNames =
    [
        "endfield_token",
        "endfield_token.txt",
        "token",
        "token.txt",
    ];

    private static readonly string[] RootFileNames = ["token.txt"];

    public static string SecretsDirectory(string repoRoot) => Path.Combine(repoRoot, ".secrets");

    public static string DefaultTokenPath(string repoRoot) =>
        Path.Combine(SecretsDirectory(repoRoot), "endfield_token.txt");

    public static bool HasToken(string repoRoot) => FindTokenFile(repoRoot) != null;

    /// <summary>Первый существующий файл в том же порядке, в котором его ищет snapshot.py.</summary>
    public static string? FindTokenFile(string repoRoot)
    {
        var secrets = SecretsDirectory(repoRoot);
        foreach (var name in FileNames)
        {
            var path = Path.Combine(secrets, name);
            if (IsUsable(path)) return path;
        }

        foreach (var name in RootFileNames)
        {
            var path = Path.Combine(repoRoot, name);
            if (IsUsable(path)) return path;
        }

        return null;
    }

    /// <summary>
    /// Пишет в активный файл, чтобы не оставить рядом старый ключ с более высоким
    /// приоритетом. Если файла ещё нет — создаёт <c>.secrets/endfield_token.txt</c>.
    /// </summary>
    public static string Save(string repoRoot, string token)
    {
        var value = token.Trim();
        if (value.Length == 0) throw new ArgumentException("Token is empty.", nameof(token));

        var directory = SecretsDirectory(repoRoot);
        Directory.CreateDirectory(directory);

        var path = FindTokenFile(repoRoot) ?? DefaultTokenPath(repoRoot);
        var temp = path + ".tmp";
        File.WriteAllText(temp, value, new UTF8Encoding(false));
        File.Move(temp, path, true);
        return path;
    }

    private static bool IsUsable(string path)
    {
        try
        {
            return File.Exists(path) && !string.IsNullOrWhiteSpace(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
