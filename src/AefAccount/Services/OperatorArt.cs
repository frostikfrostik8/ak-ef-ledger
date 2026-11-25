using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AefAccount.Services;

public enum OperatorArtKind
{
    Icon,
    Portrait,
    Detail,
    Full,
}

public sealed record OperatorIdentity(string Slug, string Ru, string En);

public static class OperatorArt
{
    private const string EmbeddedSlug = "vesper";

    private static readonly Dictionary<string, OperatorIdentity> Identities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Akekuri"] = new("akekuri", "Акэкури", "Akekuri"),
        ["Alesh"] = new("alesh", "Алеш", "Alesh"),
        ["Antal"] = new("antal", "Антал", "Antal"),
        ["Arclight"] = new("arclight", "Арклайт", "Arclight"),
        ["Ardelia"] = new("ardelia", "Арделия", "Ardelia"),
        ["Avywenna"] = new("avywenna", "Авивенна", "Avywenna"),
        ["Camille"] = new("camille", "Камиль", "Camille"),
        ["Catcher"] = new("catcher", "Кэтчер", "Catcher"),
        ["Chen Qianyu"] = new("chen", "Чэнь Цяньюй", "Chen Qianyu"),
        ["Da Pan"] = new("dapan", "Да Пан", "Da Pan"),
        ["Ember"] = new("ember", "Эмбер", "Ember"),
        ["Endministrator"] = new("endministrator1", "Эндминистратор", "Endministrator"),
        ["Estella"] = new("estella", "Эстелла", "Estella"),
        ["Fluorite"] = new("fluorite", "Флюорит", "Fluorite"),
        ["Gilberta"] = new("gilberta", "Гилберта", "Gilberta"),
        ["Si"] = new("jsspsi", "Си", "Si"),
        ["Laevatain"] = new("laevatain", "Лэватейн", "Laevatain"),
        ["Last Rite"] = new("lastrite", "Панихида", "Last Rite"),
        ["Lifeng"] = new("lifeng", "Лифэн", "Lifeng"),
        ["Liino"] = new("liino", "Лиино", "Liino"),
        ["Arcane"] = new("lizhiyan", "Премудрость", "Arcane"),
        ["Mi Fu"] = new("mifu", "Ми Фу", "Mi Fu"),
        ["Mimino"] = new("mimino", "Мимино", "Mimino"),
        ["Argent Flow"] = new("minghe", "Серебристый Поток", "Argent Flow"),
        ["Perlica"] = new("perlica", "Перлика", "Perlica"),
        ["Pogranichnik"] = new("pogranichnik", "Пограничник", "Pogranichnik"),
        ["Purrchena"] = new("purrche", "Муррчена", "Purrchena"),
        ["Rossi"] = new("rossi", "Росси", "Rossi"),
        ["Snowshine"] = new("snowshine", "Светоснежка", "Snowshine"),
        ["Kay"] = new("sweetpie", "Кей", "Kay"),
        ["Tangtang"] = new("tangtang", "Тантан", "Tangtang"),
        ["Typhoeus"] = new("typhoea", "Тифея", "Typhoeus"),
        ["Wulfgard"] = new("wulfgard", "Вулфгард", "Wulfgard"),
        ["Xaihi"] = new("xaihi", "Сайхи", "Xaihi"),
        ["Yvonne"] = new("yvonne", "Ивонна", "Yvonne"),
        ["Zhuang Fangyi"] = new("zhuangfy", "Чжуан Фанъи", "Zhuang Fangyi"),
        ["Vesper"] = new("vesper", "Веспер", "Vesper"),
    };

    private static readonly Dictionary<string, OperatorIdentity> Aliases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, OperatorIdentity> BySlug = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, OperatorIdentity> ByCharId = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, BitmapImage?> Cache = new();
    private static readonly ConcurrentDictionary<string, string?> Paths = new();

    private static string? _root;
    private static string? _apiCacheRoot;

    static OperatorArt()
    {
        foreach (var identity in Identities.Values) BySlug.TryAdd(identity.Slug, identity);
    }

    public static string? Root => _root;

    public static string? ApiCacheRoot => _apiCacheRoot;

    public static bool HasApiCache { get; private set; }

    /// <summary>
    /// Активная папка арта и manifest уже прочитаны; сбрасываем кэш и добавляем aliases.
    /// <paramref name="apiCacheRoot"/> задаётся один раз при старте приложения;
    /// последующие вызовы без аргумента сохраняют его.
    /// </summary>
    public static void Initialize(string? apiCacheRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(apiCacheRoot))
            _apiCacheRoot = Path.GetFullPath(apiCacheRoot);

        _root = ArtPack.Status.Root;
        Cache.Clear();
        Paths.Clear();
        Aliases.Clear();
        BySlug.Clear();
        ByCharId.Clear();
        foreach (var identity in Identities.Values) BySlug.TryAdd(identity.Slug, identity);
        MergeManifest();

        HasApiCache = false;
        if (!string.IsNullOrWhiteSpace(_apiCacheRoot) && Directory.Exists(_apiCacheRoot))
        {
            foreach (var directory in Directory.EnumerateDirectories(_apiCacheRoot))
            {
                if (File.Exists(Path.Combine(directory, "icon.png")) ||
                    File.Exists(Path.Combine(directory, "portrait.png")) ||
                    File.Exists(Path.Combine(directory, "full.png")))
                {
                    HasApiCache = true;
                    break;
                }
            }
        }
    }

    public static OperatorIdentity? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (Identities.TryGetValue(name, out var identity)) return identity;
        if (Aliases.TryGetValue(name.Trim(), out identity)) return identity;
        return BySlug.TryGetValue(name.Trim(), out identity) ? identity : null;
    }

    public static OperatorIdentity? ResolveByCharId(string? charId)
    {
        if (string.IsNullOrWhiteSpace(charId)) return null;
        return ByCharId.TryGetValue(charId.Trim(), out var identity) ? identity : null;
    }

    public static string RuName(string? name) => Resolve(name)?.Ru ?? name ?? "";

    public static string EnglishName(string? name) => Resolve(name)?.En ?? name ?? "";

    public static string? Slug(string? name) => Resolve(name)?.Slug;

    public static string? Slug(string? name, string? charId) =>
        Resolve(name)?.Slug ?? ResolveByCharId(charId)?.Slug;

    public static bool HasLocalArt(string? name) => Has(name, null, OperatorArtKind.Portrait);

    public static bool Has(string? name, OperatorArtKind kind) => Has(name, null, kind);

    public static bool Has(string? name, string? charId, OperatorArtKind kind)
    {
        var identity = Resolve(name) ?? ResolveByCharId(charId);
        var slug = identity?.Slug;
        if (ResolvePath(slug, charId, kind) != null) return true;
        return slug != null && slug.Equals(EmbeddedSlug, StringComparison.OrdinalIgnoreCase);
    }

    public static ImageSource? Load(string? name, OperatorArtKind kind) => Load(name, null, kind);

    public static ImageSource? Load(string? name, string? charId, OperatorArtKind kind)
    {
        var identity = Resolve(name) ?? ResolveByCharId(charId);
        var slug = identity?.Slug;

        var cacheKey = $"{slug ?? "-"}|{charId ?? "-"}|{kind}";
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached;

        var image = LoadKind(slug, charId, kind);
        Cache[cacheKey] = image;
        return image;
    }

    private static void MergeManifest()
    {
        var manifest = ArtPack.Manifest;
        if (manifest?.Operators == null) return;

        foreach (var op in manifest.Operators)
        {
            if (string.IsNullOrWhiteSpace(op.Slug)) continue;
            var slug = op.Slug.Trim();
            var charId = string.IsNullOrWhiteSpace(op.CharId) ? null : op.CharId.Trim();

            var identity = BySlug.TryGetValue(slug, out var known)
                ? known
                : new OperatorIdentity(
                    slug,
                    op.Ru ?? op.En ?? op.Name ?? slug,
                    op.En ?? op.Name ?? slug);

            if (!string.IsNullOrWhiteSpace(op.Ru)) identity = identity with { Ru = op.Ru.Trim() };
            if (!string.IsNullOrWhiteSpace(op.En)) identity = identity with { En = op.En.Trim() };

            BySlug[slug] = identity;
            if (charId != null) ByCharId[charId] = identity;

            foreach (var alias in new[] { op.Name, op.En, op.Ru })
            {
                var name = alias?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !Identities.ContainsKey(name))
                    Aliases[name] = identity;
            }
        }
    }

    private static BitmapImage? LoadKind(string? slug, string? charId, OperatorArtKind kind)
    {
        var path = ResolvePath(slug, charId, kind);
        if (path != null) return LoadFromPath(path, DecodeWidth(kind));
        return slug == null ? null : LoadEmbedded(slug, kind);
    }

    private static string? ResolvePath(string? slug, string? charId, OperatorArtKind kind)
    {
        var cacheKey = $"{slug ?? "-"}|{charId ?? "-"}|{kind}";
        if (Paths.TryGetValue(cacheKey, out var cached)) return cached;

        string? path = null;

        // 1. Curated local art pack: by slug, then by charId folder.
        if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
        {
            if (!string.IsNullOrWhiteSpace(slug))
                path = FindInRoot(_root, slug, kind);
            if (path == null && !string.IsNullOrWhiteSpace(charId))
                path = FindInRoot(_root, charId, kind);
        }

        // 2. API cache written by snapshot.py: data/art_cache/<char_id>/.
        if (path == null && !string.IsNullOrWhiteSpace(_apiCacheRoot) &&
            !string.IsNullOrWhiteSpace(charId) && Directory.Exists(_apiCacheRoot))
        {
            path = FindInRoot(_apiCacheRoot, charId, kind, CacheFallbacks(kind));
        }

        Paths[cacheKey] = path;
        return path;
    }

    private static string? FindInRoot(string root, string folder, OperatorArtKind kind, IEnumerable<OperatorArtKind>? fallbacks = null)
    {
        foreach (var candidate in fallbacks ?? Fallbacks(kind))
        {
            var file = Path.Combine(root, folder, FileName(candidate) + ".png");
            if (File.Exists(file)) return file;
        }
        return null;
    }

    private static IEnumerable<OperatorArtKind> Fallbacks(OperatorArtKind kind) => kind switch
    {
        OperatorArtKind.Full => [OperatorArtKind.Full, OperatorArtKind.Detail, OperatorArtKind.Icon],
        OperatorArtKind.Detail => [OperatorArtKind.Detail, OperatorArtKind.Portrait, OperatorArtKind.Icon],
        OperatorArtKind.Portrait => [OperatorArtKind.Portrait, OperatorArtKind.Icon],
        _ => [OperatorArtKind.Icon, OperatorArtKind.Portrait],
    };

    private static IEnumerable<OperatorArtKind> CacheFallbacks(OperatorArtKind kind) => kind switch
    {
        OperatorArtKind.Icon => [OperatorArtKind.Icon, OperatorArtKind.Full],
        OperatorArtKind.Full => [OperatorArtKind.Full, OperatorArtKind.Portrait, OperatorArtKind.Icon],
        OperatorArtKind.Detail => [OperatorArtKind.Detail, OperatorArtKind.Portrait, OperatorArtKind.Full, OperatorArtKind.Icon],
        _ => [OperatorArtKind.Portrait, OperatorArtKind.Full, OperatorArtKind.Icon],
    };

    private static string FileName(OperatorArtKind kind) => kind switch
    {
        OperatorArtKind.Icon => "icon",
        OperatorArtKind.Full => "full",
        _ => "portrait",
    };

    private static int DecodeWidth(OperatorArtKind kind) => kind switch
    {
        OperatorArtKind.Icon => 128,
        OperatorArtKind.Portrait => 320,
        OperatorArtKind.Detail => 640,
        _ => 1000,
    };

    private static BitmapImage? LoadFromPath(string path, int decodeWidth)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.DecodePixelWidth = decodeWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or UriFormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Встроенная Веспер — страховка для чистой сборки без папки assets.</summary>
    private static BitmapImage? LoadEmbedded(string slug, OperatorArtKind kind)
    {
        if (!slug.Equals(EmbeddedSlug, StringComparison.OrdinalIgnoreCase)) return null;

        foreach (var candidate in Fallbacks(kind))
        {
            var file = FileName(candidate);
            var suffix = $".Operators.{slug}.{file}.png";
            var resource = typeof(OperatorArt).Assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (resource == null) continue;

            try
            {
                using var stream = typeof(OperatorArt).Assembly.GetManifestResourceStream(resource);
                if (stream == null) continue;

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = DecodeWidth(candidate);
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception exception) when (exception is IOException or NotSupportedException or ArgumentException or UnauthorizedAccessException)
            {
                // Пробуем следующий вариант.
            }
        }
        return null;
    }
}
