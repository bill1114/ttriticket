namespace TtriTicket.Services;

public class GoogleSheetsOptions
{
    public const string SectionName = "GoogleSheets";

    public string SpreadsheetId { get; set; } = string.Empty;
    public string SheetGid { get; set; } = "0";
    public int CacheMinutes { get; set; } = 5;

    public string NameColumn { get; set; } = "姓名/職編";
    public string IntroductionColumn { get; set; } = "請以20字內短文";
    public string PhotoColumn { get; set; } = "請上傳投稿照片";

    // 投票紀錄工作表（同一試算表內另一個分頁）
    public string VotesSheetGid { get; set; } = string.Empty;
    public int VotesCacheSeconds { get; set; } = 30;
    public string VoteEmployeeIdColumn { get; set; } = "職編";
    public string VoteCandidateIdColumn { get; set; } = "候選人ID";
    public string VoteCandidateNameColumn { get; set; } = "候選人姓名";

    // Google Apps Script 網址（用於寫入投票紀錄）
    public string VoteAppendWebhookUrl { get; set; } = string.Empty;
}
