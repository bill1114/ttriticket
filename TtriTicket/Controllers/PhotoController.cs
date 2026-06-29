using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TtriTicket.Services;

namespace TtriTicket.Controllers;

public class PhotoController : Controller
{
    private readonly GoogleDriveImageService _driveImageService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PhotoController> _logger;

    public PhotoController(
        GoogleDriveImageService driveImageService,
        IMemoryCache cache,
        ILogger<PhotoController> logger)
    {
        _driveImageService = driveImageService;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    [Route("Photo/Drive")]
    [Route("Photo/Drive/{fileId}")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Drive(string? fileId, [FromQuery] string? id, CancellationToken cancellationToken)
    {
        fileId = (fileId ?? id)?.Trim();
        if (string.IsNullOrWhiteSpace(fileId) || fileId.Length > 100)
        {
            return BadRequest();
        }

        var cacheKey = $"drive-photo:{fileId}";
        if (_cache.TryGetValue(cacheKey, out (byte[] Data, string ContentType) cached))
        {
            return File(cached.Data, cached.ContentType);
        }

        var result = await _driveImageService.TryFetchImageAsync(fileId, cancellationToken);
        if (result is null)
        {
            _logger.LogWarning("圖片無法載入 FileId={FileId}", fileId);
            return Redirect("/images/photo-unavailable.svg");
        }

        _cache.Set(cacheKey, result.Value, TimeSpan.FromHours(6));
        return File(result.Value.Data, result.Value.ContentType);
    }
}
