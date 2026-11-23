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

    private static readonly ConcurrentDictionary<string, BitmapImage?> Cache = new();

    public static OperatorIdentity? Resolve(string? name) =>
        !string.IsNullOrWhiteSpace(name) && Identities.TryGetValue(name, out var identity) ? identity : null;

    public static string RuName(string? name) => Resolve(name)?.Ru ?? name ?? "";

    public static string EnglishName(string? name) => Resolve(name)?.En ?? name ?? "";

    public static string? Slug(string? name) => Resolve(name)?.Slug;

    public static bool HasLocalArt(string? name) => Resolve(name) != null;

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

    private static BitmapImage? LoadKind(string slug, OperatorArtKind kind)
    {
        foreach (var candidate in Fallbacks(kind))
        {
            var image = LoadFile(slug, candidate);
            if (image != null) return image;
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

    private static BitmapImage? LoadFile(string slug, OperatorArtKind kind)
    {
        var file = kind switch
        {
            OperatorArtKind.Icon => "icon",
            OperatorArtKind.Full => "full",
            _ => "portrait",
        };
        var decodeWidth = kind switch
        {
            OperatorArtKind.Icon => 128,
            OperatorArtKind.Portrait => 320,
            OperatorArtKind.Detail => 640,
            _ => 1000,
        };

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.DecodePixelWidth = decodeWidth;
            image.UriSource = new Uri(
                $"pack://application:,,,/Assets/Operators/{slug}/{file}.png",
                UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or UriFormatException)
        {
            return null;
        }
    }
}
