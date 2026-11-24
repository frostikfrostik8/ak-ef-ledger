using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AefAccount.Localization;

/// <summary>Элемент списка языков. Auto — следовать языку системы.</summary>
public sealed class LanguageOption : INotifyPropertyChanged
{
    public string Code { get; init; } = "";
    public string? NameKey { get; init; }
    public string NativeName { get; init; } = "";

    public string DisplayName => NameKey is null ? NativeName : L.T(NameKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
}

/// <summary>
/// Локализация интерфейса. Строки лежат во внешних файлах <c>lang/*.json</c>
/// рядом с exe; встроенные RU/EN — резерв, если файлов нет.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public const string AutoCode = "auto";
    private const string FallbackLanguage = "en";
    private const string BaseLanguage = "ru";

    private static readonly Lazy<LocalizationService> LazyInstance =
        new(() => new LocalizationService(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static LocalizationService Instance => LazyInstance.Value;

    private sealed record LanguagePack(
        string Code,
        string Name,
        string? CultureName,
        string? DateFormat,
        string? DateFormatShort,
        Dictionary<string, string> Strings);

    private readonly Dictionary<string, LanguagePack> _embeddedPacks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _embeddedJson = new(StringComparer.OrdinalIgnoreCase);
    private volatile Dictionary<string, LanguagePack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _systemLanguage;
    private string _preference = AutoCode;
    private string _current = BaseLanguage;
    private CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");

    private LocalizationService()
    {
        var ui = CultureInfo.CurrentUICulture;
        _systemLanguage = string.IsNullOrWhiteSpace(ui.Name) ? "en-US" : ui.Name;

        LanguageDirectory = Path.Combine(AppContext.BaseDirectory, "lang");

        LoadEmbedded();
        EnsureLanguageFiles();
        LoadExternalInto(_packs);
        RebuildLanguageOptions();

        var savedPreference = AppSettings.LoadLanguagePreference();
        _preference = NormalizePreference(savedPreference ?? AutoCode);
        if (savedPreference is not null && !savedPreference.Equals(_preference, StringComparison.OrdinalIgnoreCase))
            AppSettings.SaveLanguagePreference(_preference);
        ApplyPreference(_preference, persist: false);
        ApplyCommandLineOverride();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;
    public event EventHandler? LanguagesChanged;

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = [];

    public string LanguageDirectory { get; }

    public string CurrentLanguage => _current;

    public string CurrentLanguageName
    {
        get
        {
            var packs = _packs;
            return packs.TryGetValue(_current, out var pack) ? pack.Name : _current;
        }
    }

    public string LanguagePreference => _preference;

    public CultureInfo Culture => _culture;

    public string DateFormat { get; private set; } = "dd MMM yyyy, HH:mm";

    public string DateFormatShort { get; private set; } = "dd MMM yyyy";

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return "";
            var packs = _packs;
            if (packs.TryGetValue(_current, out var current) &&
                current.Strings.TryGetValue(key, out var value)) return value;
            if (packs.TryGetValue(FallbackLanguage, out var fallback) &&
                fallback.Strings.TryGetValue(key, out var fallbackValue)) return fallbackValue;
            if (packs.TryGetValue(BaseLanguage, out var basePack) &&
                basePack.Strings.TryGetValue(key, out var baseValue)) return baseValue;
            return key;
        }
    }

    public string Format(string key, params object?[] args)
    {
        var template = this[key];
        if (args.Length == 0) return template;
        try
        {
            return string.Format(_culture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public void SetPreference(string code)
    {
        var normalized = NormalizePreference(code);
        ApplyPreference(normalized, persist: true);
    }

    /// <summary>Перечитывает <c>lang/*.json</c> без перезапуска приложения.</summary>
    public void Reload()
    {
        var packs = new Dictionary<string, LanguagePack>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _embeddedPacks)
        {
            packs[pair.Key] = pair.Value with
            {
                Strings = new Dictionary<string, string>(pair.Value.Strings, StringComparer.Ordinal),
            };
        }
        LoadExternalInto(packs);
        _packs = packs;
        RebuildLanguageOptions();
        _preference = NormalizePreference(_preference);
        LanguagesChanged?.Invoke(this, EventArgs.Empty);
        ApplyPreference(_preference, persist: false);
    }

    /// <summary>Создаёт папку lang и недостающие файлы из встроенных RU/EN.</summary>
    public void EnsureLanguageFiles()
    {
        try
        {
            Directory.CreateDirectory(LanguageDirectory);
            foreach (var pair in _embeddedJson)
            {
                var path = Path.Combine(LanguageDirectory, pair.Key + ".json");
                if (!File.Exists(path))
                    File.WriteAllText(path, pair.Value, new UTF8Encoding(false));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Папка только для чтения — работаем на встроенных строках.
        }
    }

    private void ApplyCommandLineOverride()
    {
        var argument = Environment.GetCommandLineArgs()
            .FirstOrDefault(arg => arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase));
        if (argument is null) return;

        var code = argument.Split('=', 2)[1].Trim();
        var normalized = NormalizePreference(code);
        if (normalized == _preference && normalized != AutoCode) return;
        _preference = normalized;
        ApplyPreference(normalized, persist: false);
    }

    private string NormalizePreference(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return AutoCode;
        code = code.Trim();
        if (code.Equals(AutoCode, StringComparison.OrdinalIgnoreCase)) return AutoCode;
        var packs = _packs;
        if (packs.ContainsKey(code)) return code;

        var language = code.Split('-', '_')[0];
        if (packs.ContainsKey(language)) return language;
        return AutoCode;
    }

    private string ResolveLanguage(string preference)
    {
        var packs = _packs;
        if (preference != AutoCode && packs.ContainsKey(preference)) return preference;

        // Auto: exact system culture (e.g. en-US), then its neutral language
        // (e.g. en). If neither has a lang/*.json file, fall through to English.
        var systemLanguage = ResolveSystemLanguage(packs);
        if (systemLanguage is not null) return systemLanguage;

        // Unsupported system language: English is the default.
        if (packs.ContainsKey(FallbackLanguage)) return FallbackLanguage;
        if (packs.ContainsKey(BaseLanguage)) return BaseLanguage;
        return packs.Keys.FirstOrDefault() ?? BaseLanguage;
    }

    private string? ResolveSystemLanguage(IReadOnlyDictionary<string, LanguagePack> packs)
    {
        if (packs.ContainsKey(_systemLanguage)) return _systemLanguage;

        var language = _systemLanguage.Split('-', '_')[0];
        if (packs.ContainsKey(language)) return language;
        return null;
    }

    private void ApplyPreference(string preference, bool persist)
    {
        _preference = preference;
        var resolved = ResolveLanguage(preference);
        var packs = _packs;
        if (!packs.TryGetValue(resolved, out var pack)) return;

        _current = pack.Code;
        _culture = CreateCulture(pack);
        DateFormat = pack.DateFormat ?? "dd MMM yyyy, HH:mm";
        DateFormatShort = pack.DateFormatShort ?? "dd MMM yyyy";

        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;

        if (persist) AppSettings.SaveLanguagePreference(preference);

        foreach (var option in AvailableLanguages) option.Refresh();

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguagePreference)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static CultureInfo CreateCulture(LanguagePack pack)
    {
        if (!string.IsNullOrWhiteSpace(pack.CultureName))
        {
            try { return CultureInfo.GetCultureInfo(pack.CultureName); }
            catch (CultureNotFoundException) { }
        }

        try { return CultureInfo.GetCultureInfo(pack.Code); }
        catch (CultureNotFoundException) { }

        return CultureInfo.InvariantCulture;
    }

    private void RebuildLanguageOptions()
    {
        var packs = _packs;
        AvailableLanguages.Clear();
        AvailableLanguages.Add(new LanguageOption { Code = AutoCode, NameKey = "lang_auto" });
        foreach (var pack in packs.Values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            AvailableLanguages.Add(new LanguageOption { Code = pack.Code, NativeName = pack.Name });
        }
    }

    private void LoadEmbedded()
    {
        var assembly = typeof(LocalizationService).Assembly;
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = resourceName.Split('.');
            if (parts.Length < 3) continue;
            var code = parts[^2];
            if (code.StartsWith("_", StringComparison.Ordinal)) continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) continue;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = reader.ReadToEnd();
            var pack = ParsePack(code, json);
            if (pack is null) continue;

            _embeddedPacks[code] = pack;
            _packs[code] = pack with
            {
                Strings = new Dictionary<string, string>(pack.Strings, StringComparer.Ordinal),
            };
            _embeddedJson[code] = json;
        }
    }

    private void LoadExternalInto(Dictionary<string, LanguagePack> packs)
    {
        if (!Directory.Exists(LanguageDirectory)) return;

        foreach (var file in Directory.EnumerateFiles(LanguageDirectory, "*.json"))
        {
            try
            {
                var code = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(code) || code.StartsWith("_", StringComparison.Ordinal)) continue;

                var json = File.ReadAllText(file, Encoding.UTF8);
                var pack = ParsePack(code, json);
                if (pack is null) continue;

                if (packs.TryGetValue(code, out var existing))
                {
                    foreach (var pair in pack.Strings) existing.Strings[pair.Key] = pair.Value;
                    packs[code] = existing with
                    {
                        Name = pack.Name == code ? existing.Name : pack.Name,
                        CultureName = pack.CultureName ?? existing.CultureName,
                        DateFormat = pack.DateFormat ?? existing.DateFormat,
                        DateFormatShort = pack.DateFormatShort ?? existing.DateFormatShort,
                    };
                }
                else
                {
                    packs[code] = pack;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // Битый или недоступный файл языка пропускаем.
            }
        }
    }

    private static LanguagePack? ParsePack(string code, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            string? name = null;
            string? culture = null;
            string? dateFormat = null;
            string? dateFormatShort = null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.StartsWith("_", StringComparison.Ordinal))
                {
                    var value = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? ""
                        : "";
                    switch (property.Name)
                    {
                        case "_languageName": name = value; break;
                        case "_culture": culture = value; break;
                        case "_dateFormat": dateFormat = value; break;
                        case "_dateFormatShort": dateFormatShort = value; break;
                    }
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                    strings[property.Name] = property.Value.GetString() ?? "";
            }

            return new LanguagePack(
                code,
                string.IsNullOrWhiteSpace(name) ? code : name,
                string.IsNullOrWhiteSpace(culture) ? null : culture,
                string.IsNullOrWhiteSpace(dateFormat) ? null : dateFormat,
                string.IsNullOrWhiteSpace(dateFormatShort) ? null : dateFormatShort,
                strings);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Короткие обращения к строкам из кода.</summary>
public static class L
{
    public static string T(string key) => LocalizationService.Instance[key];

    public static string T(string key, params object?[] args) =>
        LocalizationService.Instance.Format(key, args);

    public static CultureInfo Culture => LocalizationService.Instance.Culture;
}
