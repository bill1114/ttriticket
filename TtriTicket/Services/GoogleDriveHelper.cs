using System.Text.RegularExpressions;

namespace TtriTicket.Services;

public static partial class GoogleDriveHelper
{
  public static string? ExtractFileId(string? url)
  {
    if (string.IsNullOrWhiteSpace(url))
    {
      return null;
    }

    var cleaned = url.Trim().Replace("\\", "");

    const string filePrefix = "/file/d/";
    var index = cleaned.IndexOf(filePrefix, StringComparison.OrdinalIgnoreCase);
    if (index >= 0)
    {
      var start = index + filePrefix.Length;
      var end = cleaned.IndexOf('/', start);
      return SanitizeFileId(end > start ? cleaned[start..end] : cleaned[start..]);
    }

    var idMatch = FileIdRegex().Match(cleaned);
    if (idMatch.Success)
    {
      return SanitizeFileId(idMatch.Groups[1].Value);
    }

    return null;
  }

  public static string ToProxyPhotoUrl(string? rawUrl)
  {
    var fileId = ExtractFileId(rawUrl);
    return string.IsNullOrEmpty(fileId) ? string.Empty : $"/Photo/Drive?id={Uri.EscapeDataString(fileId)}";
  }

  private static string? SanitizeFileId(string fileId)
  {
    var trimmed = fileId.Trim();
    return trimmed.Length is > 0 and <= 100 ? trimmed : null;
  }

  [GeneratedRegex(@"(?:[?&]id=|/d/)([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase)]
  private static partial Regex FileIdRegex();
}
