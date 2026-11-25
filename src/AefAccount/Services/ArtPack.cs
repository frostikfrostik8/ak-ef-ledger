using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using AefAccount.Localization;

namespace AefAccount.Services;

/// <summary>Состояние папки с артом: путь, количество операторов и объём.</summary>
public sealed record ArtPackStatus(
    string? Root,
    int OperatorCount,
    int FileCount,
    long TotalBytes,
    string? Version,
    string? GeneratedAt,
    string? Source)
{
    public static ArtPackStatus None { get; } = new(null, 0, 0, 0, null, null, null);

    public bool IsInstalled => OperatorCount > 0;
    public double Megabytes => TotalBytes / 1048576.0;
}

public sealed class ArtManifest
{
    public int Schema { get; set; }
    public string? Version { get; set; }
    public string? GeneratedAt { get; set; }
    public string? Source { get; set; }
    public List<ArtManifestOperator> Operators { get; set; } = [];
}

public sealed class ArtManifestOperator
{
    public string Slug { get; set; } = "";
    public string? CharId { get; set; }
    public string? Name { get; set; }
    public string? Ru { get; set; }
    public string? En { get; set; }
    public Dictionary<string, ArtManifestFile>? Files { get; set; }
}

public sealed class ArtManifestFile
{
    public int W { get; set; }
    public int H { get; set; }
    public long Bytes { get; set; }
    public string? Sha256 { get; set; }
}

/// <summary>Ссылки на официальный арт-пак в GitHub Releases.</summary>
public static class ArtPackCatalog
{
    public const string Repository = "frostikfrostik8/ak-ef-ledger";
    public const string ReleaseTag = "art-v1";
    public const string AssetName = "art-v1.zip";

    public static string DownloadUrl =>
        $"https://github.com/{Repository}/releases/download/{ReleaseTag}/{AssetName}";

    public static string ReleaseUrl =>
        $"https://github.com/{Repository}/releases/tag/{ReleaseTag}";
}

/// <summary>
/// Внешняя папка с артом оперативников.
/// Порядок поиска: --art-dir → своя папка в настройках → %LOCALAPPDATA% → assets рядом с exe.
/// </summary>
public static class ArtPack
{
    private const string ManifestFileName = "manifest.json";
    private const string TestSlug = "vesper";
    private static readonly string[] ArtFiles = ["icon.png", "portrait.png", "full.png"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    static ArtPack()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("AkEfLedger/0.4");
    }

    public static string LocalRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AkEfLedger",
        "art",
        "operators");

    public static string ExeRoot { get; } =
        Path.Combine(AppContext.BaseDirectory, "assets", "operators");

    public static string? OverrideRoot { get; private set; }
    public static string? ActiveRoot => Status.Root;
    public static ArtPackStatus Status { get; private set; } = ArtPackStatus.None;
    public static ArtManifest? Manifest { get; private set; }

    public static void Initialize(string? overrideRoot = null)
    {
        OverrideRoot = string.IsNullOrWhiteSpace(overrideRoot) ? null : overrideRoot;
        Refresh();
    }

    /// <summary>Пересобирает состояние: ищет активную папку и читает manifest.json.</summary>
    public static ArtPackStatus Refresh()
    {
        var custom = AppSettings.LoadArtDirectory();
        var root = ResolveRoot(custom);

        Manifest = root == null ? null : ReadManifest(root);
        Status = root == null ? ArtPackStatus.None : Describe(root);
        return Status;
    }

    /// <summary>Использовать существующую папку без копирования (например, снапшот сайта).</summary>
    public static ArtPackStatus UseFolder(string folder)
    {
        var full = Path.GetFullPath(folder);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);
        if (CountOperators(full) == 0)
            throw new InvalidDataException("art_not_found");

        AppSettings.SaveArtDirectory(full);
        return Refresh();
    }

    /// <summary>Убрать выбранную папку или удалить скачанный пак. Папку рядом с exe не трогаем.</summary>
    public static ArtPackStatus Remove()
    {
        AppSettings.SaveArtDirectory(null);
        if (Directory.Exists(LocalRoot))
        {
            try
            {
                Directory.Delete(LocalRoot, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Пак мог быть занят другим процессом; состояние всё равно перечитываем.
            }
        }
        return Refresh();
    }

    /// <summary>Распаковать art-v1.zip (manifest.json + operators/) или папку с артом в локальный кэш.</summary>
    public static ArtPackStatus InstallFromZip(string zipPath)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException(zipPath);

        var staging = LocalRoot + ".tmp";
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);

        try
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var prefix = DetectPrefix(archive);

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    var relative = entry.FullName.Replace('\\', '/');
                    if (prefix.Length > 0 && !relative.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    relative = relative[prefix.Length..];
                    if (relative.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)) continue;

                    // В zip арт лежит в operators/, но корень установки — сам operators.
                    if (relative.StartsWith("operators/", StringComparison.OrdinalIgnoreCase))
                        relative = relative["operators/".Length..];

                    var destination = Path.Combine(staging, relative);
                    EnsureInside(staging, destination);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination, true);
                }
            }

            if (CountOperators(staging) == 0)
                throw new InvalidDataException("art_not_found");

            VerifyHashes(staging);
            ReplaceDirectory(staging, LocalRoot);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }

        AppSettings.SaveArtDirectory(null);
        return Refresh();
    }

    /// <summary>Скачать art-v1.zip из GitHub Releases во временный файл.</summary>
    public static async Task<string> DownloadAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var target = Path.Combine(Path.GetTempPath(), $"aef-art-{Guid.NewGuid():N}.zip");
        using var response = await Http.GetAsync(
            ArtPackCatalog.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = File.Create(target);
        var buffer = new byte[81920];
        long read = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            read += count;
            if (total > 0) progress?.Report((double)read / total);
        }
        progress?.Report(1);
        return target;
    }

    private static string? ResolveRoot(string? custom)
    {
        if (TryRoot(OverrideRoot, out var overridden)) return overridden;
        if (TryRoot(custom, out var selected)) return selected;
        if (TryRoot(LocalRoot, out var downloaded)) return downloaded;
        if (TryRoot(ExeRoot, out var portable)) return portable;
        return null;
    }

    private static bool TryRoot(string? path, out string root)
    {
        root = "";
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
        if (CountOperators(path) == 0) return false;
        root = Path.GetFullPath(path);
        return true;
    }

    private static int CountOperators(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return 0;
        try
        {
            var count = 0;
            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                if (Path.GetFileName(directory).Equals(TestSlug, StringComparison.OrdinalIgnoreCase)) continue;
                if (ArtFiles.Any(file => File.Exists(Path.Combine(directory, file)))) count++;
            }
            return count;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static ArtPackStatus Describe(string root)
    {
        var operators = 0;
        var files = 0;
        long bytes = 0;
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                if (Path.GetFileName(directory).Equals(TestSlug, StringComparison.OrdinalIgnoreCase)) continue;

                var hasArt = false;
                foreach (var file in Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly))
                {
                    hasArt = true;
                    files++;
                    bytes += new FileInfo(file).Length;
                }
                if (hasArt) operators++;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Часть папок может быть недоступна — показываем то, что удалось прочитать.
        }

        var manifest = Manifest;
        return new ArtPackStatus(
            root,
            operators,
            files,
            bytes,
            manifest?.Version,
            manifest?.GeneratedAt,
            manifest?.Source);
    }

    private static ArtManifest? ReadManifest(string root)
    {
        var path = Path.Combine(root, ManifestFileName);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<ArtManifest>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string DetectPrefix(ZipArchive archive)
    {
        if (archive.GetEntry(ManifestFileName) != null) return "";

        var topDirectories = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .Where(name => name.Contains('/'))
            .Select(name => name[..name.IndexOf('/')])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (topDirectories.Count == 1)
        {
            var prefix = topDirectories[0] + "/";
            if (archive.GetEntry(prefix + ManifestFileName) != null) return prefix;
            if (archive.Entries.Any(entry =>
                    entry.FullName.Replace('\\', '/').StartsWith(prefix + "operators/", StringComparison.OrdinalIgnoreCase)))
                return prefix;
        }
        return "";
    }

    private static void VerifyHashes(string staging)
    {
        var manifest = ReadManifest(staging);
        if (manifest?.Operators == null) return;

        foreach (var op in manifest.Operators)
        {
            if (op.Files == null) continue;
            foreach (var (kind, file) in op.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Sha256)) continue;
                var path = Path.Combine(staging, op.Slug, kind + ".png");
                if (!File.Exists(path)) throw new InvalidDataException("art_file_missing");
                using var stream = File.OpenRead(path);
                var hash = Convert.ToHexString(SHA256.HashData(stream));
                if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("art_hash_mismatch");
            }
        }
    }

    private static void ReplaceDirectory(string source, string target)
    {
        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        var backup = target + ".bak";
        TryDelete(backup);
        if (Directory.Exists(target)) Directory.Move(target, backup);

        try
        {
            Directory.Move(source, target);
            TryDelete(backup);
        }
        catch
        {
            if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
            throw;
        }
    }

    private static void EnsureInside(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("art_bad_zip_path");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Временная папка не критична: её перезапишет следующая установка.
        }
    }
}
