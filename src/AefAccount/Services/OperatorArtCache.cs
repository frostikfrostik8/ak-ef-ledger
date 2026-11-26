using System.IO;
using System.Net.Http;
using System.Text.Json;
using AefAccount.Models;

namespace AefAccount.Services;

/// <summary>Что именно нужно догрузить для оператора.</summary>
public sealed record PendingArt(OperatorModel Operator, bool NeedIcon, bool NeedFull);

/// <summary>
/// Локальный кэш артов из API. Сначала проверяет локальный арт-пак и уже
/// скачанный кэш; если арта нет — скачивает avatar/illustration по URL из
/// снимка в data/art_cache/&lt;char_id&gt;/.
/// </summary>
public static class OperatorArtCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] PngHeader = [0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];
    private static readonly byte[] PngEnd = [0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];

    private static string? _root;

    static OperatorArtCache()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("AkEfLedger/0.4");
    }

    public static string? Root => _root;

    public static void Initialize(string? root)
    {
        if (!string.IsNullOrWhiteSpace(root))
            _root = Path.GetFullPath(root);
    }

    /// <summary>
    /// Считает очередь на UI-потоке: <see cref="OperatorArt"/> использует
    /// обычные словари, которые нельзя читать из фонового потока.
    /// </summary>
    public static IReadOnlyList<PendingArt> CollectPending(IEnumerable<OperatorModel> operators)
    {
        if (string.IsNullOrWhiteSpace(_root)) return [];

        var result = new List<PendingArt>();
        foreach (var op in operators)
        {
            var charId = op.CharId?.Trim();
            if (!IsSafeFolder(charId)) continue;

            // Курируемый пак важнее API-кэша: если в нём есть хоть какой-то
            // арт, отдельные icon/full из API не догружаем.
            if (OperatorArt.HasArtPackArt(op.Name, charId, OperatorArtKind.Portrait)) continue;

            var iconUrl = string.IsNullOrWhiteSpace(op.AvatarUrl) ? null : op.AvatarUrl.Trim();
            var fullUrl = string.IsNullOrWhiteSpace(op.IllustrationUrl)
                ? iconUrl
                : op.IllustrationUrl.Trim();
            var folder = Path.Combine(_root, charId!);

            // Нужно не только «файла нет», но и «файл не подтверждён URL».
            // Это лечит старые/ложные source.json и битые PNG в фоне.
            var needIcon = iconUrl != null &&
                           (!OperatorArt.HasExact(op.Name, op.CharId, OperatorArtKind.Icon) ||
                            !IsSourceCurrent(folder, "icon", iconUrl));
            var needFull = fullUrl != null &&
                           (!OperatorArt.HasExact(op.Name, op.CharId, OperatorArtKind.Full) ||
                            !IsSourceCurrent(folder, "full", fullUrl));

            if (needIcon || needFull) result.Add(new PendingArt(op, needIcon, needFull));
        }

        return result
            .GroupBy(item => item.Operator.CharId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    /// <summary>
    /// Догружает отсутствующие арты. Вызывать из фонового потока; очередь
    /// должна быть получена через <see cref="CollectPending"/> на UI-потоке.
    /// Ошибки сети не являются ошибкой приложения.
    /// </summary>
    public static async Task EnsureAsync(IReadOnlyList<PendingArt> pending, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_root) || pending.Count == 0) return;

        using var gate = new SemaphoreSlim(4);
        var tasks = pending.Select(async item =>
        {
            try
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await EnsureOneAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Отмена — штатный путь при смене снимка/закрытии.
            }
            catch (Exception)
            {
                // Битая ссылка/сеть: остаётся локальный fallback или буква.
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task EnsureOneAsync(PendingArt pending, CancellationToken cancellationToken)
    {
        var op = pending.Operator;
        var charId = op.CharId?.Trim();
        if (!IsSafeFolder(charId)) return;

        var folder = Path.Combine(_root!, charId!);
        var iconPath = Path.Combine(folder, "icon.png");
        var fullPath = Path.Combine(folder, "full.png");
        var iconUrl = string.IsNullOrWhiteSpace(op.AvatarUrl) ? null : op.AvatarUrl.Trim();
        var fullUrl = string.IsNullOrWhiteSpace(op.IllustrationUrl) ? iconUrl : op.IllustrationUrl.Trim();

        var source = ReadSource(folder);
        var changed = false;

        var iconDownloaded = false;
        var iconFailed = false;
        if (pending.NeedIcon && iconUrl != null)
        {
            try
            {
                iconDownloaded = await DownloadAsync(iconUrl, iconPath, cancellationToken).ConfigureAwait(false);
                iconFailed = !iconDownloaded;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                iconFailed = true;
            }
        }

        var fullDownloaded = false;
        var fullFailed = false;
        if (pending.NeedFull && fullUrl != null)
        {
            try
            {
                fullDownloaded = await DownloadAsync(fullUrl, fullPath, cancellationToken).ConfigureAwait(false);
                fullFailed = !fullDownloaded;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                fullFailed = true;
            }
        }

        // Отмена после загрузки не должна писать provenance: следующая пачка
        // или snapshot.py сами разберутся с файлом.
        cancellationToken.ThrowIfCancellationRequested();

        changed |= UpdateSourceEntry(source, "icon", iconPath, iconUrl, iconDownloaded, iconFailed);
        changed |= UpdateSourceEntry(source, "full", fullPath, fullUrl, fullDownloaded, fullFailed);

        if (changed) WriteSource(folder, source);
    }

    private static bool UpdateSourceEntry(
        Dictionary<string, string> source,
        string kind,
        string path,
        string? url,
        bool downloaded,
        bool failed)
    {
        var had = source.TryGetValue(kind, out var old);

        // Provenance пишем только после того, как файл реально существует
        // и является PNG. Иначе невалидный URL выглядел бы как успешная
        // загрузка, и snapshot.py потом не перекачал бы файл.
        var fileValid = File.Exists(path) && IsValidPng(path);

        if (downloaded && !string.IsNullOrWhiteSpace(url) && fileValid)
        {
            if (had && old == url) return false;
            source[kind] = url;
            return true;
        }

        // В снимке нет URL для этого вида: валидный файл и его старый
        // provenance не трогаем — иначе потеряем подтверждение зря.
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Загрузка упала, но старый файл и его provenance целы — не врём,
        // что файл соответствует новому URL. snapshot.py сам обновит его.
        if (failed && had && fileValid) return false;

        // Файл уже есть, source совпадает с URL и PNG валиден — подтверждено.
        if (had && old == url && fileValid) return false;

        // Провенанс неизвестен или устарел: запись убираем, файл не трогаем.
        // Тогда snapshot.py увидит расхождение и перекачает файл.
        if (had)
        {
            source.Remove(kind);
            return true;
        }

        return false;
    }

    private static bool IsSourceCurrent(string folder, string kind, string url)
    {
        var path = Path.Combine(folder, kind + ".png");
        if (!File.Exists(path) || !IsValidPng(path)) return false;

        var source = ReadSource(folder);
        return source.TryGetValue(kind, out var recorded) &&
               recorded.Equals(url, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadSource(string folder)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(folder, "source.json");
        if (!File.Exists(path)) return result;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return result;

            // EnumerateObject терпит null-значения и дубли ключей (в т.ч. с
            // разным регистром), которые раньше роняли Dictionary-конструктор.
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String) continue;
                var value = property.Value.GetString();
                if (string.IsNullOrWhiteSpace(value)) continue;
                result[property.Name] = value.Trim();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or ArgumentException)
        {
            result.Clear();
        }

        return result;
    }

    private static void WriteSource(string folder, Dictionary<string, string> source)
    {
        var path = Path.Combine(folder, "source.json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(folder);
            var json = JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true });
            if (File.Exists(path) && File.ReadAllText(path) == json) return;

            File.WriteAllText(temporary, json);
            File.Move(temporary, path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);
            // source.json не критичен: при следующем запуске всё пересчитается.
        }
    }

    private static async Task<bool> DownloadAsync(string? url, string targetPath, CancellationToken cancellationToken)
    {
        if (!TryValidateUrl(url, out var uri)) return false;

        var folder = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(folder)) return false;
        Directory.CreateDirectory(folder);

        var temporary = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // HttpClient следует redirect'ам; проверяем конечный адрес, чтобы
            // обещание «только https + skport.com» не обходилось редиректом.
            if (response.RequestMessage?.RequestUri is { } finalUri &&
                !TryValidateUrl(finalUri.ToString(), out _))
            {
                throw new InvalidDataException("unexpected redirect");
            }

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = File.Create(temporary))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            if (new FileInfo(temporary).Length == 0)
                throw new InvalidDataException("empty image");

            if (!IsValidPng(temporary))
                throw new InvalidDataException("not a PNG");

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, targetPath, true);
            return File.Exists(targetPath) && IsValidPng(targetPath);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static bool IsValidPng(string path)
    {
        try
        {
            // Не только 8-байтовая сигнатура: обрезанный файл с одной
            // сигнатурой раньше считался валидным и закреплял provenance.
            // Требуем минимальную длину, первый чанк IHDR и финальный IEND.
            var minimum = PngSignature.Length + PngHeader.Length + PngEnd.Length;
            if (new FileInfo(path).Length < minimum) return false;

            using var stream = File.OpenRead(path);
            var signature = new byte[PngSignature.Length];
            stream.ReadExactly(signature);
            if (!signature.AsSpan().SequenceEqual(PngSignature)) return false;

            var header = new byte[PngHeader.Length];
            stream.ReadExactly(header);
            if (!header.AsSpan().SequenceEqual(PngHeader)) return false;

            stream.Seek(-PngEnd.Length, SeekOrigin.End);
            var end = new byte[PngEnd.Length];
            stream.ReadExactly(end);
            return end.AsSpan().SequenceEqual(PngEnd);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or EndOfStreamException)
        {
            return false;
        }
    }

    private static bool IsSafeFolder(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value != "." && value != ".." &&
        value.IndexOfAny(['/', '\\', ':']) < 0;

    private static bool TryValidateUrl(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (!parsed.Host.Equals("skport.com", StringComparison.OrdinalIgnoreCase) &&
            !parsed.Host.EndsWith(".skport.com", StringComparison.OrdinalIgnoreCase))
            return false;
        uri = parsed;
        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Временный файл не критичен.
        }
    }
}
