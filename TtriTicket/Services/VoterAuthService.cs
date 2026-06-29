namespace TtriTicket.Services;

public class VoterAuthService : IVoterAuthService
{
    private const string SessionEmployeeIdKey = "EmployeeId";
    private const string SessionEmployeeNameKey = "EmployeeName";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public VoterAuthService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<(bool Success, string? Name, string? ErrorMessage)> ValidateAndSignInAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = EmployeeIdNormalizer.Normalize(employeeId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return Task.FromResult<(bool, string?, string?)>((false, null, "請輸入職編。"));
        }

        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return Task.FromResult<(bool, string?, string?)>((false, null, "登入失敗，請稍後再試。"));
        }

        session.SetString(SessionEmployeeIdKey, normalizedId);
        session.SetString(SessionEmployeeNameKey, normalizedId);
        return Task.FromResult<(bool, string?, string?)>((true, normalizedId, null));
    }

    public void SignOut()
    {
        _httpContextAccessor.HttpContext?.Session.Clear();
    }

    public string? GetSignedInEmployeeId() =>
        _httpContextAccessor.HttpContext?.Session.GetString(SessionEmployeeIdKey);

    public string? GetSignedInEmployeeName() =>
        _httpContextAccessor.HttpContext?.Session.GetString(SessionEmployeeNameKey);

    public bool IsSignedIn() => !string.IsNullOrWhiteSpace(GetSignedInEmployeeId());
}
