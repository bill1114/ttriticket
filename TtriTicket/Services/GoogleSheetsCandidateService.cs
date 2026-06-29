using System.Globalization;
using System.Net;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TtriTicket.Models;

namespace TtriTicket.Services;

public class GoogleSheetsCandidateService : ICandidateService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly GoogleSheetsOptions _options;
    private readonly ILogger<GoogleSheetsCandidateService> _logger;
    private const string CacheKey = "candidates";

    public bool IsUsingLiveData { get; private set; }
    public string? ConnectionMessage { get; private set; }
    public string? ConnectionHint { get; private set; }

    public GoogleSheetsCandidateService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<GoogleSheetsOptions> options,
        ILogger<GoogleSheetsCandidateService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public void ClearCache() => _cache.Remove(CacheKey);

    public async Task<IReadOnlyList<Candidate>> GetCandidatesAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (forceRefresh || _options.CacheMinutes <= 0)
        {
            ClearCache();
        }

        if (_options.CacheMinutes <= 0)
        {
            return await FetchCandidatesFromSheetAsync(cancellationToken);
        }

        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes);
            return await FetchCandidatesFromSheetAsync(cancellationToken);
        }) ?? [];
    }

    public async Task<Candidate?> GetCandidateByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var candidates = await GetCandidatesAsync(cancellationToken: cancellationToken);
        return candidates.FirstOrDefault(c => c.Id == id);
    }

    public async Task<SheetConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var isConfigured = IsSpreadsheetConfigured();

        if (!isConfigured)
        {
            return new SheetConnectionResult
            {
                Success = false,
                IsConfigured = false,
                ErrorMessage = "SpreadsheetId 尚未設定",
                Hint = "請在 appsettings.Local.json 填入試算表 ID"
            };
        }

        try
        {
            var download = await TryDownloadSheetCsvAsync(cancellationToken);
            if (!download.Success)
            {
                return new SheetConnectionResult
                {
                    Success = false,
                    IsConfigured = true,
                    SpreadsheetId = _options.SpreadsheetId,
                    SheetGid = _options.SheetGid,
                    HttpStatusCode = download.StatusCode,
                    ErrorMessage = download.ErrorMessage,
                    Hint = download.Hint
                };
            }

            await using var stream = download.Stream!;
            var (candidates, headers) = await ParseCandidatesFromCsvAsync(stream, cancellationToken);

            return new SheetConnectionResult
            {
                Success = candidates.Count > 0,
                IsConfigured = true,
                SpreadsheetId = _options.SpreadsheetId,
                SheetGid = _options.SheetGid,
                HttpStatusCode = download.StatusCode,
                CandidateCount = candidates.Count,
                CandidateNames = candidates.Select(c => c.Name).ToList(),
                ColumnHeaders = headers,
                ErrorMessage = candidates.Count == 0 ? "連線成功但沒有候選人資料" : null,
                Hint = candidates.Count == 0 ? "請確認試算表有回應資料，且「姓名/職編」欄位有填寫" : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "試算表連線測試失敗");
            return new SheetConnectionResult
            {
                Success = false,
                IsConfigured = true,
                SpreadsheetId = _options.SpreadsheetId,
                SheetGid = _options.SheetGid,
                ErrorMessage = ex.Message,
                Hint = "請確認網路連線，以及試算表已設為公開可讀"
            };
        }
    }

    private async Task<List<Candidate>> FetchCandidatesFromSheetAsync(CancellationToken cancellationToken)
    {
        if (!IsSpreadsheetConfigured())
        {
            _logger.LogWarning("Google Sheets SpreadsheetId 尚未設定，使用示範資料");
            IsUsingLiveData = false;
            ConnectionMessage = "尚未設定 Google 試算表 ID";
            ConnectionHint = "請在 appsettings.json 或 appsettings.Local.json 填入 SpreadsheetId";
            return GetDemoCandidates();
        }

        try
        {
            var download = await TryDownloadSheetCsvAsync(cancellationToken);
            if (!download.Success)
            {
                IsUsingLiveData = false;
                ConnectionMessage = $"Google 試算表串接失敗：{download.ErrorMessage}";
                ConnectionHint = download.Hint;
                _logger.LogError("無法讀取試算表：{Error}", download.ErrorMessage);
                return [];
            }

            var stream = download.Stream!;
            await using (stream)
            {
                var (candidates, _) = await ParseCandidatesFromCsvAsync(stream, cancellationToken);

                if (candidates.Count == 0)
                {
                    IsUsingLiveData = false;
                    ConnectionMessage = "已連接試算表，但沒有有效的候選人資料";
                    ConnectionHint = "請確認「姓名/職編」欄位有填寫";
                    return [];
                }

                IsUsingLiveData = true;
                ConnectionMessage = $"串接成功，已載入 {candidates.Count} 位候選人";
                ConnectionHint = null;
                return candidates;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "從 Google Sheets 讀取候選人資料失敗");
            IsUsingLiveData = false;
            ConnectionMessage = $"Google 試算表串接失敗：{ex.Message}";
            ConnectionHint = ex is TaskCanceledException
                ? "連線 Google 逾時，請稍後按「同步最新資料」重試"
                : "請確認試算表已設為「知道連結的使用者 → 檢視者」";
            return [];
        }
    }

    private bool IsSpreadsheetConfigured() =>
        !string.IsNullOrWhiteSpace(_options.SpreadsheetId) &&
        _options.SpreadsheetId != "YOUR_SPREADSHEET_ID";

    private IEnumerable<string> BuildCsvExportUrls()
    {
        var id = _options.SpreadsheetId;
        var gid = _options.SheetGid;
        yield return $"https://docs.google.com/spreadsheets/d/{id}/export?format=csv&gid={gid}";
        yield return $"https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&gid={gid}";
        yield return $"https://docs.google.com/spreadsheets/d/{id}/pub?gid={gid}&single=true&output=csv";
    }

    private async Task<SheetDownloadResult> TryDownloadSheetCsvAsync(CancellationToken cancellationToken)
    {
        HttpStatusCode? lastStatus = null;
        string? lastError = null;

        foreach (var url in BuildCsvExportUrls())
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                lastStatus = response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)response.StatusCode}";
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var stream = new MemoryStream(bytes);
                return new SheetDownloadResult
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Stream = stream
                };
            }
            catch (Exception ex)
            {
                lastError = ex is TaskCanceledException
                    ? "連線 Google 試算表逾時"
                    : ex.Message;
            }
        }

        var statusCode = lastStatus.HasValue ? (int)lastStatus.Value : (int?)null;
        var isTimeout = lastError?.Contains("逾時", StringComparison.Ordinal) == true ||
                        lastError?.Contains("canceled", StringComparison.OrdinalIgnoreCase) == true;
        return new SheetDownloadResult
        {
            Success = false,
            StatusCode = statusCode,
            ErrorMessage = lastError ?? "無法讀取試算表",
            Hint = statusCode is 401 or 403
                ? "請將試算表設為「知道連結的使用者 → 檢視者」"
                : isTimeout
                    ? "網路連線 Google 較慢或暫時無法連線，請稍後按「同步最新資料」重試"
                    : "請確認 SpreadsheetId 與 SheetGid 是否正確"
        };
    }

    private sealed class SheetDownloadResult
    {
        public bool Success { get; init; }
        public int? StatusCode { get; init; }
        public Stream? Stream { get; init; }
        public string? ErrorMessage { get; init; }
        public string? Hint { get; init; }
    }

    private async Task<(List<Candidate> Candidates, List<string> Headers)> ParseCandidatesFromCsvAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null
        };

        using var csv = new CsvReader(reader, config);
        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord?
            .Select(NormalizeHeader)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList() ?? [];

        var candidates = new List<Candidate>();
        var id = 1;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawHeader in csv.HeaderRecord ?? [])
            {
                var normalizedHeader = NormalizeHeader(rawHeader);
                if (!string.IsNullOrWhiteSpace(normalizedHeader))
                {
                    dict[normalizedHeader] = csv.GetField(rawHeader) ?? string.Empty;
                }
            }

            var name = GetFieldValue(dict, _options.NameColumn);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            candidates.Add(new Candidate
            {
                Id = id++,
                Name = name,
                Introduction = GetFieldValue(dict, _options.IntroductionColumn),
                PhotoUrl = NormalizePhotoUrl(GetFieldValue(dict, _options.PhotoColumn))
            });
        }

        return (candidates, headers);
    }

    private static string NormalizeHeader(string header) =>
        header.Trim().TrimStart('\ufeff').Trim('"');

    private static string GetFieldValue(IDictionary<string, object> dict, string columnName)
    {
        // 1. 完全比對
        if (dict.TryGetValue(columnName, out var exact) && exact != null)
        {
            return exact.ToString()?.Trim() ?? string.Empty;
        }

        var trimmedKeyword = columnName.Trim();

        // 2. 不分大小寫完全比對
        var caseInsensitiveMatch = dict.Keys.FirstOrDefault(k =>
            string.Equals(k.Trim(), trimmedKeyword, StringComparison.OrdinalIgnoreCase));
        if (caseInsensitiveMatch != null && dict[caseInsensitiveMatch] != null)
        {
            return dict[caseInsensitiveMatch].ToString()?.Trim() ?? string.Empty;
        }

        // 3. 欄位標題包含關鍵字（Google 表單題目較長時仍可比對）
        var containsMatch = dict.Keys.FirstOrDefault(k =>
            k.Contains(trimmedKeyword, StringComparison.OrdinalIgnoreCase) ||
            trimmedKeyword.Contains(k.Trim(), StringComparison.OrdinalIgnoreCase));
        if (containsMatch != null && dict[containsMatch] != null)
        {
            return dict[containsMatch].ToString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string NormalizePhotoUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var firstUrl = raw.Split(['\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Replace("\\", ""))
            .FirstOrDefault(s => s.StartsWith("http", StringComparison.OrdinalIgnoreCase)) ?? raw.Trim().Replace("\\", "");

        if (!firstUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (firstUrl.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase) ||
            firstUrl.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase))
        {
            return GoogleDriveHelper.ToProxyPhotoUrl(firstUrl);
        }

        return firstUrl;
    }

    private static string? ExtractGoogleDriveFileId(string url) => GoogleDriveHelper.ExtractFileId(url);

    private static List<Candidate> GetDemoCandidates() =>
    [
        new Candidate
        {
            Id = 1,
            Name = "曾莉雯",
            Introduction = "測試測試",
            PhotoUrl = "https://via.placeholder.com/300x300?text=%E6%9B%BE%E8%8E%89%E9%9B%AF"
        },
        new Candidate
        {
            Id = 2,
            Name = "測試",
            Introduction = "如文",
            PhotoUrl = "https://via.placeholder.com/300x300?text=%E6%B8%AC%E8%A9%A6"
        },
        new Candidate
        {
            Id = 3,
            Name = "林奕昇/1524",
            Introduction = "000",
            PhotoUrl = "https://via.placeholder.com/300x300?text=1524"
        }
    ];
}
