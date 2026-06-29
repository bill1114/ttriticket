using Microsoft.AspNetCore.Mvc;
using TtriTicket.Models;
using TtriTicket.Services;

namespace TtriTicket.Controllers;

public class AccountController : Controller
{
    private readonly IVoterAuthService _authService;

    public AccountController(IVoterAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_authService.IsSignedIn())
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.EmployeeId))
        {
            model.ErrorMessage = "請輸入職編。";
            return View(model);
        }

        var (success, _, errorMessage) = await _authService.ValidateAndSignInAsync(
            model.EmployeeId,
            cancellationToken);

        if (!success)
        {
            model.ErrorMessage = errorMessage;
            return View(model);
        }

        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _authService.SignOut();
        return RedirectToAction("Login");
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl) &&
            !returnUrl.Equals("/", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Vote");
    }
}
