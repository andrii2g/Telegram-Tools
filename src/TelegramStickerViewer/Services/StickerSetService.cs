using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using a2g.TelegramStickerViewer.Models;
using TL;

namespace a2g.TelegramStickerViewer.Services;

public sealed class StickerSetService
{
    private readonly TelegramClientFactory _factory;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<long, string> _fileIdToUniqueId = new();

    public StickerSetService(TelegramClientFactory factory, IMemoryCache cache)
    {
        _factory = factory;
        _cache = cache;
    }

    public async Task<StickerSetModel?> GetStickerSetAsync(string shortName, CancellationToken ct)
    {
        var cacheKey = $"stickerset:{shortName}";
        if (_cache.TryGetValue(cacheKey, out StickerSetModel? cached) && cached is not null)
            return cached;

        var client = await _factory.GetClientAsync(ct);
        await _factory.LoginIfNeededAsync(ct);

        Messages_StickerSet raw;
        try
        {
            raw = await client.Messages_GetStickerSet(
                new InputStickerSetShortName { short_name = shortName },
                0);
        }
        catch
        {
            return null;
        }

        var stickers = new List<StickerModel>();

        foreach (var doc in raw.documents.OfType<Document>())
        {
            _documents[doc.id] = doc;

            var stickerAttr = doc.attributes?.OfType<DocumentAttributeSticker>().FirstOrDefault();
            var imageSize = doc.attributes?.OfType<DocumentAttributeImageSize>().FirstOrDefault();
            var videoAttr = doc.attributes?.OfType<DocumentAttributeVideo>().FirstOrDefault();

            var isAnimated = string.Equals(doc.mime_type, "application/x-tgsticker", StringComparison.OrdinalIgnoreCase);
            var isVideo = string.Equals(doc.mime_type, "video/webm", StringComparison.OrdinalIgnoreCase);

            stickers.Add(new StickerModel
            {
                FileId = doc.id.ToString(),
                AccessHash = doc.access_hash.ToString(),
                FileReference = Convert.ToBase64String(doc.file_reference),
                MimeType = doc.mime_type,
                Size = doc.size,
                Width = imageSize?.w ?? videoAttr?.w ?? 0,
                Height = imageSize?.h ?? videoAttr?.h ?? 0,
                Emoji = stickerAttr?.alt,
                IsAnimated = isAnimated,
                IsVideo = isVideo,
                MediaUrl = $"/api/stickers/file/{doc.id}"
            });
        }

        var documents = raw.documents.OfType<Document>().ToList();
        var model = new StickerSetModel
        {
            Id = raw.set.id,
            AccessHash = raw.set.access_hash,
            ShortName = raw.set.short_name,
            Title = raw.set.title,
            Count = raw.set.count,
            IsAnimated = documents.Any(d =>
                string.Equals(d.mime_type, "application/x-tgsticker", StringComparison.OrdinalIgnoreCase)),
            IsVideo = documents.Any(d =>
                string.Equals(d.mime_type, "video/webm", StringComparison.OrdinalIgnoreCase)),
            Stickers = stickers
        };

        _cache.Set(cacheKey, model, TimeSpan.FromMinutes(10));
        return model;
    }

    public async Task<StickerFileResult?> GetFileStreamAsync(string fileId, CancellationToken ct)
    {
        if (!long.TryParse(fileId, out var documentId))
            return null;

        var client = await _factory.GetClientAsync(ct);
        await _factory.LoginIfNeededAsync(ct);

        // We need the document again with full metadata/file_reference.
        // For simplicity, search cache first through already loaded packs.
        var doc = await FindDocumentInCacheAsync(documentId, ct);
        if (doc is null)
            return null;

        var tempDir = Path.Combine(AppContext.BaseDirectory, "data", "cache");
        Directory.CreateDirectory(tempDir);

        var ext = GuessExtension(doc.mime_type);
        var tempPath = Path.Combine(tempDir, $"{doc.id}{ext}");

        if (!File.Exists(tempPath))
        {
            await using var fs = File.Create(tempPath);
            await client.DownloadFileAsync(doc, fs);
        }

        var stream = File.OpenRead(tempPath);

        return new StickerFileResult
        {
            Stream = stream,
            ContentType = GuessContentType(doc.mime_type),
            FileName = $"{doc.id}{ext}"
        };
    }

    private async Task<Document?> FindDocumentInCacheAsync(long documentId, CancellationToken ct)
    {
        var client = await _factory.GetClientAsync(ct);

        // Minimal approach:
        // re-request from a short-name index if you keep it,
        // or keep docs in an in-memory dictionary.
        // For now, scan memory cache entries is not supported directly,
        // so production code should maintain a second dictionary.
        //
        // Simple demo storage:
        if (_documents.TryGetValue(documentId, out var doc))
            return doc;

        return await Task.FromResult<Document?>(null);
    }

    private readonly ConcurrentDictionary<long, Document> _documents = new();

    public async Task PrimeStickerSetAsync(string shortName, CancellationToken ct)
    {
        var client = await _factory.GetClientAsync(ct);
        await _factory.LoginIfNeededAsync(ct);

        var raw = await client.Messages_GetStickerSet(
            new InputStickerSetShortName { short_name = shortName },
            0);

        foreach (var doc in raw.documents.OfType<Document>())
            _documents[doc.id] = doc;
    }

    private static string GuessContentType(string? mimeType) =>
        mimeType switch
        {
            "image/webp" => "image/webp",
            "video/webm" => "video/webm",
            "application/x-tgsticker" => "application/x-tgsticker",
            _ => "application/octet-stream"
        };

    private static string GuessExtension(string? mimeType) =>
        mimeType switch
        {
            "image/webp" => ".webp",
            "video/webm" => ".webm",
            "application/x-tgsticker" => ".tgs",
            _ => ".bin"
        };
}

public sealed class StickerFileResult
{
    public Stream Stream { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "file.bin";
}
