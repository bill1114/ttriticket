namespace TtriTicket.Services;

public class SheetConnectionResult
{
    public bool Success { get; init; }
    public bool IsConfigured { get; init; }
    public string SpreadsheetId { get; init; } = string.Empty;
    public string SheetGid { get; init; } = string.Empty;
    public int? HttpStatusCode { get; init; }
    public int CandidateCount { get; init; }
    public List<string> CandidateNames { get; init; } = [];
    public List<string> ColumnHeaders { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public string? Hint { get; init; }
}
