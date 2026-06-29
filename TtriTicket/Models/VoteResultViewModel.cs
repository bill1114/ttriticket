namespace TtriTicket.Models;

public class VoteResultViewModel
{
    public string Title { get; set; } = "投票系統";
    public List<Candidate> Candidates { get; set; } = [];
    public int TotalVotes { get; set; }
    public bool HasVoted { get; set; }
    public bool IsUsingDemoData { get; set; }
    public bool IsConnectionSuccess { get; set; }
    public string? ConnectionMessage { get; set; }
    public string? ConnectionHint { get; set; }
    public string? SignedInEmployeeId { get; set; }
    public string? SignedInEmployeeName { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
}
