using System.IO;
using System.Text.Json;

namespace AefAccount.Localization;

/// <summary>
/// Небольшие пользовательские настройки в %LOCALAPPDATA%\AkEfLedger\settings.json.
/// Файл переживает переустановку приложения и не требует прав на папку exe.
/// Настройки хранятся как «ключ → строка», чтобы новые ключи не стирали старые.
/// </summary>
internal static class AppSettings
{
    private const string LanguageKey = "language";
    private const string ArtDirectoryKey = "artDir";
    private const string AutoArtKey = "autoArtDownload";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AkEfLedger",
        "settings.json");

    public static string? LoadLanguagePreference() => Read(LanguageKey);

    public static void SaveLanguagePreference(string code) => Write(LanguageKey, code);

    public static string? LoadArtDirectory() => Read(ArtDirectoryKey);

    public static void SaveArtDirectory(string? path) => Write(ArtDirectoryKey, path);

    /// <summary>По умолчанию выключено: приложение не ходит в сеть без согласия.</summary>
    public static bool LoadAutoDownloadArt() =>
        string.Equals(Read(AutoArtKey), "true", StringComparison.OrdinalIgnoreCase);

    public static void SaveAutoDownloadArt(bool value) =>
        Write(AutoArtKey, value ? "true" : null);

    private static string? Read(string key)
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(key, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static void Write(string key, string? value)
    {
        try
        {
            var values = LoadAll();
            if (string.IsNullOrWhiteSpace(value)) values.Remove(key);
            else values[key] = value;

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var temporary = FilePath + ".tmp";
            using (var stream = File.Create(temporary))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var (name, text) in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    writer.WriteString(name, text);
                writer.WriteEndObject();
            }
            File.Move(temporary, FilePath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Настройка не критична: при следующем запуске снова сработает автоопределение.
        }
    }

    private static Dictionary<string, string> LoadAll()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (!File.Exists(FilePath)) return values;
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return values;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                    values[property.Name] = property.Value.GetString() ?? "";
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Битый файл настроек перезапишется при следующем сохранении.
        }
        return values;
    }
}
