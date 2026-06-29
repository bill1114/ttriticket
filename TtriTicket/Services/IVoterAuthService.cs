namespace TtriTicket.Services;

public interface IVoterAuthService
{
    Task<(bool Success, string? Name, string? ErrorMessage)> ValidateAndSignInAsync(
        string employeeId,
        CancellationToken cancellationToken = default);

    void SignOut();
    string? GetSignedInEmployeeId();
    string? GetSignedInEmployeeName();
    bool IsSignedIn();
}
