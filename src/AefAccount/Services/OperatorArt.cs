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

    private static readonly Dictionary<string, OperatorIdentity> BySlug = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, BitmapImage?> Cache = new();
    private static readonly ConcurrentDictionary<string, string?> Paths = new();

    private static string? _root;

    static OperatorArt()
    {
        foreach (var identity in Identities.Values) BySlug.TryAdd(identity.Slug, identity);
    }

    public static string? Root => _root;

    /// <summary>Активная папка и manifest уже прочитаны; сбрасываем кэш и добавляем aliases.</summary>
    public static void Initialize()
    {
        _root = ArtPack.Status.Root;
        Cache.Clear();
        Paths.Clear();
        MergeManifest();
    }

    public static OperatorIdentity? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (Identities.TryGetValue(name, out var identity)) return identity;
        return BySlug.TryGetValue(name.Trim(), out identity) ? identity : null;
    }

    public static string RuName(string? name) => Resolve(name)?.Ru ?? name ?? "";

    public static string EnglishName(string? name) => Resolve(name)?.En ?? name ?? "";

    public static string? Slug(string? name) => Resolve(name)?.Slug;

    public static bool HasLocalArt(string? name) => Has(name, OperatorArtKind.Portrait);

    public static bool Has(string? name, OperatorArtKind kind)
    {
        var identity = Resolve(name);
        if (identity == null) return false;
        if (ResolvePath(identity.Slug, kind) != null) return true;
        return identity.Slug.Equals(EmbeddedSlug, StringComparison.OrdinalIgnoreCase);
    }

    public static ImageSource? Load(string? name, OperatorArtKind kind)
    {
        var identity = Resolve(name);
        if (identity == null) return null;

        var cacheKey = $"{identity.Slug}:{kind}";
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached;

        var image = LoadKind(identity.Slug, kind);
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
            var name = op.Name ?? op.En ?? op.Ru;
            if (string.IsNullOrWhiteSpace(name) || Identities.ContainsKey(name)) continue;

            var english = op.En ?? name;
            var russian = op.Ru ?? english;
            var identity = new OperatorIdentity(slug, russian, english);
            Identities[name] = identity;
            BySlug.TryAdd(slug, identity);
        }
    }

    private static BitmapImage? LoadKind(string slug, OperatorArtKind kind)
    {
        var path = ResolvePath(slug, kind);
        if (path != null) return LoadFromPath(path, DecodeWidth(kind));
        return LoadEmbedded(slug, kind);
    }

    private static string? ResolvePath(string slug, OperatorArtKind kind)
    {
        var cacheKey = $"{slug}:{kind}";
        if (Paths.TryGetValue(cacheKey, out var cached)) return cached;

        string? path = null;
        if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
        {
            foreach (var candidate in Fallbacks(kind))
            {
                var file = Path.Combine(_root, slug, FileName(candidate) + ".png");
                if (File.Exists(file))
                {
                    path = file;
                    break;
                }
            }
        }

        Paths[cacheKey] = path;
        return path;
    }

    private static IEnumerable<OperatorArtKind> Fallbacks(OperatorArtKind kind) => kind switch
    {
        OperatorArtKind.Full => [OperatorArtKind.Full, OperatorArtKind.Detail, OperatorArtKind.Icon],
        OperatorArtKind.Detail => [OperatorArtKind.Detail, OperatorArtKind.Portrait, OperatorArtKind.Icon],
        OperatorArtKind.Portrait => [OperatorArtKind.Portrait, OperatorArtKind.Icon],
        _ => [OperatorArtKind.Icon, OperatorArtKind.Portrait],
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
