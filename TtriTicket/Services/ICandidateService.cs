using TtriTicket.Models;

namespace TtriTicket.Services;

public interface ICandidateService
{
    bool IsUsingLiveData { get; }
    string? ConnectionMessage { get; }
    string? ConnectionHint { get; }
    void ClearCache();
    Task<IReadOnlyList<Candidate>> GetCandidatesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
    Task<Candidate?> GetCandidateByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SheetConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
