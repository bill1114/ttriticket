namespace TtriTicket.Services;



public interface IVoteService

{

    Task<bool> HasVotedAsync(string employeeId, CancellationToken cancellationToken = default);

    Task<VoteWriteResult> TryVoteAsync(

        string employeeId,

        int candidateId,

        string candidateName,

        bool allowMultipleVotes,

        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, int>> GetVoteCountsAsync(CancellationToken cancellationToken = default);

    Task<int> GetTotalVotesAsync(CancellationToken cancellationToken = default);

    Task<VotePageStats> GetPageStatsAsync(string? employeeId, CancellationToken cancellationToken = default);

    void ApplyVoteCounts(IList<Models.Candidate> candidates, IReadOnlyDictionary<int, int> voteCounts);

    void ClearCache();

}



public record VotePageStats(

    IReadOnlyDictionary<int, int> VoteCounts,

    IReadOnlyDictionary<string, int> VoteCountsByName,

    int TotalVotes,

    bool HasVoted);

public record VoteWriteResult(bool Success, string? ErrorMessage);

