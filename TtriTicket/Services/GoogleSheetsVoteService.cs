using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TtriTicket.Services;

public class GoogleSheetsVoteService : IVoteService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly GoogleSheetsOptions _options;
    private readonly ILogger<GoogleSheetsVoteService> _logger;
    private const string VotesCacheKey = "vote-records";

    public GoogleSheetsVoteService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<GoogleSheetsOptions> options,
        ILogger<GoogleSheetsVoteService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> HasVotedAsync(string employeeId, CancellationToken cancellationToken = default)
    {
        var records = await GetVoteRecordsAsync(cancellationToken);
        var normalizedId = EmployeeIdNormalizer.Normalize(employeeId);
        return records.Any(r => EmployeeIdNormalizer.Equals(r.EmployeeId, normalizedId));
    }

    public async Task<VoteWriteResult> TryVoteAsync(
        string employeeId,
        int candidateId,
        string candidateName,
        bool allowMultipleVotes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.VoteAppendWebhookUrl))
        {
            _logger.LogError("VoteAppendWebhookUrl 尚未設定，無法寫入投票紀錄");
            return new VoteWriteResult(false, "VoteAppendWebhookUrl 尚未設定");
        }

        if (!allowMultipleVotes && await HasVotedAsync(employeeId, cancellationToken))
        {
            return new VoteWriteResult(false, "已投票");
        }

        var payload = new
        {
            employeeId = EmployeeIdNormalizer.Normalize(employeeId),
            candidateId,
            candidateName
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            var (statusCode, body) = await PostToAppsScriptAsync(json, cancellationToken);
            _logger.LogInformation("Apps Script 回應 HTTP {Status}: {Body}", statusCode, body);

            if (!TryParseVoteSuccess(body, out var errorMessage))
            {
                _logger.LogError("投票寫入失敗: {Error}", errorMessage);
                return new VoteWriteResult(false, errorMessage);
            }

            _cache.Remove(VotesCacheKey);
            return new VoteWriteResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "寫入 Google 試算表投票紀錄失敗");
            return new VoteWriteResult(false, ex.Message);
        }
    }

    public async Task<IReadOnlyDictionary<int, int>> GetVoteCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await GetVoteRecordsAsync(cancellationToken);
        return records
            .GroupBy(r => r.CandidateId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<int> GetTotalVotesAsync(CancellationToken cancellationToken = default)
    {
        var records = await GetVoteRecordsAsync(cancellationToken);
        return records.Count;
    }

    public async Task<VotePageStats> GetPageStatsAsync(
        string? employeeId,
        CancellationToken cancellationToken = default)
    {
        var records = await GetVoteRecordsAsync(cancellationToken);
        var voteCounts = records
            .Where(r => r.CandidateId > 0)
            .GroupBy(r => r.CandidateId)
            .ToDictionary(g => g.Key, g => g.Count());

        var voteCountsByName = records
            .Where(r => !string.IsNullOrWhiteSpace(r.CandidateName))
            .GroupBy(r => r.CandidateName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var normalizedEmployeeId = string.IsNullOrWhiteSpace(employeeId)
            ? string.Empty
            : EmployeeIdNormalizer.Normalize(employeeId);
        var hasVoted = !string.IsNullOrWhiteSpace(normalizedEmployeeId) &&
                       records.Any(r => EmployeeIdNormalizer.Equals(r.EmployeeId, normalizedEmployeeId));

        return new VotePageStats(voteCounts, voteCountsByName, records.Count, hasVoted);
    }

    public void ApplyVoteCounts(IList<Models.Candidate> candidates, IReadOnlyDictionary<int, int> voteCounts)
    {
        foreach (var candidate in candidates)
        {
            candidate.VoteCount = voteCounts.GetValueOrDefault(candidate.Id, 0);
        }
    }

    public void ClearCache() => _cache.Remove(VotesCacheKey);

    private async Task<List<ParsedVoteRecord>> GetVoteRecordsAsync(CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(VotesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.VotesCacheSeconds);
            return await FetchVoteRecordsFromSheetAsync(cancellationToken);
        }) ?? [];
    }

    private async Task<List<ParsedVoteRecord>> FetchVoteRecordsFromSheetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SpreadsheetId) ||
            string.IsNullOrWhiteSpace(_options.VotesSheetGid))
        {
            _logger.LogWarning("VotesSheetGid 尚未設定，無法讀取投票紀錄");
            return [];
        }

        try
        {
            var download = await TryDownloadVotesCsvAsync(cancellationToken);
            if (!download.Success)
            {
                _logger.LogError("讀取投票紀錄失敗 HTTP {Status}: {Error}", download.StatusCode, download.ErrorMessage);
                return [];
            }

            await using var stream = download.Stream!;
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);

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
            var headers = csv.HeaderRecord?.Select(NormalizeHeader).ToList() ?? [];
            _logger.LogInformation("投票紀錄 CSV 欄位：{Headers}", string.Join(", ", headers));

            var records = new List<ParsedVoteRecord>();
            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawEmployeeId = GetField(csv, headers, _options.VoteEmployeeIdColumn);
                if (string.IsNullOrWhiteSpace(rawEmployeeId))
                {
                    rawEmployeeId = GetFieldByIndex(csv, 1);
                }

                var candidateIdText = GetField(csv, headers, _options.VoteCandidateIdColumn);
                if (string.IsNullOrWhiteSpace(candidateIdText))
                {
                    candidateIdText = GetFieldByIndex(csv, 2);
                }

                var candidateName = GetField(csv, headers, _options.VoteCandidateNameColumn);
                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    candidateName = GetFieldByIndex(csv, 3);
                }

                TryParseSheetInt(candidateIdText, out var candidateId);

                var employeeId = EmployeeIdNormalizer.Normalize(rawEmployeeId);
                if (string.IsNullOrWhiteSpace(employeeId))
                {
                    _logger.LogWarning(
                        "略過投票紀錄：職編無法讀取（請確認試算表 B 欄為純文字，596 與 D596 須分開儲存）");
                    continue;
                }

                records.Add(new ParsedVoteRecord
                {
                    EmployeeId = employeeId,
                    CandidateId = candidateId,
                    CandidateName = candidateName
                });
            }

            _logger.LogInformation("已讀取 {Count} 筆投票紀錄", records.Count);
            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析投票紀錄 CSV 失敗");
            return [];
        }
    }

    private static string GetField(CsvReader csv, List<string> headers, string keyword)
    {
        var match = headers.FirstOrDefault(h =>
            h.Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
            h.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return string.Empty;
        }

        var rawHeader = csv.HeaderRecord?.FirstOrDefault(h => NormalizeHeader(h) == match);
        return rawHeader is null ? string.Empty : csv.GetField(rawHeader)?.Trim() ?? string.Empty;
    }

    private static string GetFieldByIndex(CsvReader csv, int index)
    {
        if (index < 0)
        {
            return string.Empty;
        }

        return csv.TryGetField(index, out string? value) ? value?.Trim() ?? string.Empty : string.Empty;
    }

    private static string NormalizeHeader(string header) =>
        header.Trim().TrimStart('\ufeff').Trim('"');

    private IEnumerable<string> BuildVotesCsvExportUrls()
    {
        var id = _options.SpreadsheetId;
        var gid = _options.VotesSheetGid;
        yield return $"https://docs.google.com/spreadsheets/d/{id}/export?format=csv&gid={gid}";
        yield return $"https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&gid={gid}";
        yield return $"https://docs.google.com/spreadsheets/d/{id}/pub?gid={gid}&single=true&output=csv";
    }

    private async Task<VotesCsvDownloadResult> TryDownloadVotesCsvAsync(CancellationToken cancellationToken)
    {
        HttpStatusCode? lastStatus = null;
        string? lastError = null;

        foreach (var url in BuildVotesCsvExportUrls())
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                lastStatus = response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)response.StatusCode} ({url})";
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                return new VotesCsvDownloadResult
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Stream = new MemoryStream(bytes)
                };
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new VotesCsvDownloadResult
        {
            Success = false,
            StatusCode = lastStatus.HasValue ? (int)lastStatus.Value : null,
            ErrorMessage = lastError ?? "無法讀取投票紀錄試算表"
        };
    }

    private static bool TryParseSheetInt(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            value = (int)Math.Round(number, MidpointRounding.AwayFromZero);
            return true;
        }

        return false;
    }

    private async Task<(int StatusCode, string Body)> PostToAppsScriptAsync(
        string json,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.VoteAppendWebhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;

        // Google Apps Script 常回 302；腳本在 POST 時執行，結果需 GET 導向網址取得
        if (statusCode is 301 or 302 or 303 or 307 or 308 &&
            response.Headers.Location is not null)
        {
            var redirectUrl = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location.ToString()
                : new Uri(new Uri(_options.VoteAppendWebhookUrl), response.Headers.Location).ToString();

            using var redirectResponse = await _httpClient.GetAsync(
                redirectUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            body = await redirectResponse.Content.ReadAsStringAsync(cancellationToken);
            statusCode = (int)redirectResponse.StatusCode;
        }

        return (statusCode, body);
    }

    private static bool TryParseVoteSuccess(string body, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(body))
        {
            errorMessage = "Apps Script 回傳空白";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("success", out var successProp) &&
                successProp.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (doc.RootElement.TryGetProperty("error", out var errorProp))
            {
                errorMessage = errorProp.GetString();
            }
            else
            {
                errorMessage = "Apps Script 回傳 success: false";
            }

            return false;
        }
        catch (JsonException)
        {
            if (body.Contains("已投票", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "已投票";
                return false;
            }

            errorMessage = body.Length > 120 ? body[..120] + "..." : body;
            return false;
        }
    }

    private sealed class ParsedVoteRecord
    {
        public string EmployeeId { get; init; } = string.Empty;
        public int CandidateId { get; init; }
        public string CandidateName { get; init; } = string.Empty;
    }

    private sealed class VotesCsvDownloadResult
    {
        public bool Success { get; init; }
        public int? StatusCode { get; init; }
        public MemoryStream? Stream { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
