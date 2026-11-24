using System.IO;
using System.Text.Json;

namespace AefAccount.Localization;

/// <summary>
/// Небольшие пользовательские настройки в %LOCALAPPDATA%\AkEfLedger\settings.json.
/// Файл переживает переустановку приложения и не требует прав на папку exe.
/// </summary>
internal static class AppSettings
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AkEfLedger",
        "settings.json");

    public static string? LoadLanguagePreference()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("language", out var language) &&
                   language.ValueKind == JsonValueKind.String
                ? language.GetString()
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void SaveLanguagePreference(string code)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            using var stream = File.Create(FilePath);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteString("language", code);
            writer.WriteEndObject();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Настройка не критична: при следующем запуске снова сработает автоопределение.
        }
    }
}
