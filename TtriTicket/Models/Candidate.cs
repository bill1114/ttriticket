namespace TtriTicket.Models;

public class Candidate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public int VoteCount { get; set; }
}
