using System.Text;
using System.Text.RegularExpressions;

namespace TtriTicket.Services;

public class GoogleDriveImageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleDriveImageService> _logger;

    private static readonly string[] UrlTemplates =
    [
        "https://drive.google.com/thumbnail?id={0}&sz=w1000",
        "https://drive.google.com/uc?export=download&id={0}",
        "https://drive.google.com/uc?export=view&id={0}",
        "https://drive.usercontent.google.com/download?id={0}&export=download",
        "https://lh3.googleusercontent.com/d/{0}=w1000"
    ];

    public GoogleDriveImageService(
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleDriveImageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(byte[] Data, string ContentType)?> TryFetchImageAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("GoogleDrive");

        foreach (var template in UrlTemplates)
        {
            var url = string.Format(template, fileId);
            var result = await TryFetchFromUrlAsync(client, url, fileId, cancellationToken);
            if (result.HasValue)
            {
                return result;
            }
        }

        _logger.LogWarning("所有來源皆無法取得 Google Drive 圖片，FileId={FileId}", fileId);
        return null;
    }

    private async Task<(byte[] Data, string ContentType)?> TryFetchFromUrlAsync(
        HttpClient client,
        string url,
        string fileId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length < 50)
            {
                return null;
            }

            var mime = DetectImageMime(bytes);
            if (mime != "application/octet-stream")
            {
                return (bytes, mime);
            }

            var html = Encoding.UTF8.GetString(bytes);
            if (!html.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var confirmToken = ExtractConfirmToken(html);
            if (string.IsNullOrEmpty(confirmToken))
            {
                return null;
            }

            var confirmUrl =
                $"https://drive.google.com/uc?export=download&confirm={confirmToken}&id={fileId}";
            using var confirmResponse = await client.GetAsync(confirmUrl, cancellationToken);
            if (!confirmResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var confirmedBytes = await confirmResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            mime = DetectImageMime(confirmedBytes);
            return mime == "application/octet-stream" ? null : (confirmedBytes, mime);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "從 {Url} 取得圖片失敗", url);
            return null;
        }
    }

    private static string? ExtractConfirmToken(string html)
    {
        var match = Regex.Match(html, @"confirm=([0-9A-Za-z_]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string DetectImageMime(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
        {
            return "image/gif";
        }

        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
        {
            return "image/webp";
        }

        return "application/octet-stream";
    }
}
