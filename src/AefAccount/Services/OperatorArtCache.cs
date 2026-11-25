using System.IO;
using System.Net.Http;
using System.Text.Json;
using AefAccount.Models;

namespace AefAccount.Services;

/// <summary>
/// Локальный кэш артов из API. Сначала проверяет локальный арт-пак и уже
/// скачанный кэш; если арта нет — скачивает avatar/illustration по URL из
/// снимка в data/art_cache/&lt;char_id&gt;/.
/// </summary>
public static class OperatorArtCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

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
    /// Догружает отсутствующие арты для операторов. Ошибки сети не являются
    /// ошибкой приложения: карточка просто останется с буквой.
    /// </summary>
    public static async Task EnsureAsync(IEnumerable<OperatorModel> operators, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_root)) return;

        var pending = operators
            .Where(op => !string.IsNullOrWhiteSpace(op.CharId))
            .Where(op => !OperatorArt.Has(op.Name, op.CharId, OperatorArtKind.Portrait))
            .GroupBy(op => op.CharId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        if (pending.Count == 0) return;

        using var gate = new SemaphoreSlim(4);
        var tasks = pending.Select(async op =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                await EnsureOneAsync(op, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Не отменяем остальные загрузки из-за одной карточки.
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
        await Task.WhenAll(tasks);
    }

    private static async Task EnsureOneAsync(OperatorModel op, CancellationToken cancellationToken)
    {
        var charId = op.CharId!.Trim();
        if (charId is "." or ".." || charId.IndexOfAny(['/', '\\']) >= 0) return;

        var folder = Path.Combine(_root!, charId);
        var iconPath = Path.Combine(folder, "icon.png");
        var fullPath = Path.Combine(folder, "full.png");

        if (!File.Exists(iconPath))
        {
            try { await DownloadAsync(op.AvatarUrl, iconPath, cancellationToken); }
            catch (Exception exception) when (exception is not OperationCanceledException) { }
        }
        if (!File.Exists(fullPath))
        {
            try { await DownloadAsync(op.IllustrationUrl ?? op.AvatarUrl, fullPath, cancellationToken); }
            catch (Exception exception) when (exception is not OperationCanceledException) { }
        }

        var source = new Dictionary<string, string>();
        if (File.Exists(iconPath) && !string.IsNullOrWhiteSpace(op.AvatarUrl))
            source["icon"] = op.AvatarUrl;
        if (File.Exists(fullPath) && !string.IsNullOrWhiteSpace(op.IllustrationUrl))
            source["full"] = op.IllustrationUrl;
        if (source.Count > 0)
        {
            var sourcePath = Path.Combine(folder, "source.json");
            var temporary = sourcePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, sourcePath, true);
        }
    }

    private static async Task DownloadAsync(string? url, string targetPath, CancellationToken cancellationToken)
    {
        if (!TryValidateUrl(url, out var uri)) return;

        var folder = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder);

        var temporary = targetPath + ".tmp";
        try
        {
            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(temporary))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            if (new FileInfo(temporary).Length == 0)
                throw new InvalidDataException("empty image");

            using (var stream = File.OpenRead(temporary))
            {
                var signature = new byte[8];
                if (stream.Read(signature, 0, signature.Length) != signature.Length ||
                    !signature.AsSpan().SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
                {
                    throw new InvalidDataException("not a PNG");
                }
            }

            File.Move(temporary, targetPath, true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

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
