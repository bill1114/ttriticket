namespace TtriTicket.Models;

public class LoginViewModel
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }
}
