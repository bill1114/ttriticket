using Microsoft.AspNetCore.Mvc;
using TtriTicket.Models;
using TtriTicket.Services;

namespace TtriTicket.Controllers;

public class VoteController : Controller
{
    private readonly ICandidateService _candidateService;
    private readonly IVoteService _voteService;
    private readonly IVoterAuthService _authService;
    private readonly IConfiguration _configuration;

    public VoteController(
        ICandidateService candidateService,
        IVoteService voteService,
        IVoterAuthService authService,
        IConfiguration configuration)
    {
        _candidateService = candidateService;
        _voteService = voteService;
        _authService = authService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(bool refresh = false, CancellationToken cancellationToken = default)
    {
        var redirect = RequireLogin();
        if (redirect is not null)
        {
            return redirect;
        }

        var employeeId = _authService.GetSignedInEmployeeId()!;

        if (refresh)
        {
            _voteService.ClearCache();
        }

        var candidatesTask = _candidateService.GetCandidatesAsync(refresh, cancellationToken);
        var statsTask = _voteService.GetPageStatsAsync(employeeId, cancellationToken);
        await Task.WhenAll(candidatesTask, statsTask);

        var candidateList = (await candidatesTask).ToList();
        var stats = await statsTask;
        _voteService.ApplyVoteCounts(candidateList, stats.VoteCounts);
        ApplyNameBasedVoteCounts(candidateList, stats.VoteCountsByName);

        var model = new VoteResultViewModel
        {
            Title = _configuration["Voting:Title"] ?? "投票系統",
            Candidates = candidateList.OrderByDescending(c => c.VoteCount).ToList(),
            TotalVotes = stats.TotalVotes,
            HasVoted = stats.HasVoted,
            IsUsingDemoData = !IsSpreadsheetConfigured(),
            IsConnectionSuccess = _candidateService.IsUsingLiveData,
            ConnectionMessage = _candidateService.ConnectionMessage,
            ConnectionHint = _candidateService.ConnectionHint,
            SignedInEmployeeId = employeeId,
            SignedInEmployeeName = _authService.GetSignedInEmployeeName()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cast(int candidateId, CancellationToken cancellationToken)
    {
        var redirect = RequireLogin();
        if (redirect is not null)
        {
            return redirect;
        }

        var employeeId = _authService.GetSignedInEmployeeId()!;
        var candidate = await _candidateService.GetCandidateByIdAsync(candidateId, cancellationToken);
        if (candidate is null)
        {
            TempData["ErrorMessage"] = "找不到該候選人。";
            return RedirectToAction(nameof(Index));
        }

        var allowMultiple = _configuration.GetValue<bool>("Voting:AllowMultipleVotes");
        var voteResult = await _voteService.TryVoteAsync(
                employeeId,
                candidateId,
                candidate.Name,
                allowMultiple,
                cancellationToken);
        if (!voteResult.Success)
        {
            TempData["ErrorMessage"] = voteResult.ErrorMessage switch
            {
                "已投票" => "您已經投過票了。",
                null or "" => "投票失敗，請確認 Google 試算表投票寫入設定是否完成。",
                var msg when msg.Contains("doPost", StringComparison.OrdinalIgnoreCase) =>
                    "Apps Script 尚未部署 doPost，請貼上腳本並重新部署。",
                var msg => $"投票失敗：{msg}"
            };
            return RedirectToAction(nameof(Index));
        }

        TempData["Message"] = $"已成功投票給 {candidate.Name}！";
        return RedirectToAction(nameof(Index), new { refresh = true });
    }

    public async Task<IActionResult> Results(bool refresh = false, CancellationToken cancellationToken = default)
    {
        var employeeId = _authService.GetSignedInEmployeeId();

        if (refresh)
        {
            _candidateService.ClearCache();
            _voteService.ClearCache();
        }

        var candidatesTask = _candidateService.GetCandidatesAsync(refresh, cancellationToken);
        var statsTask = _voteService.GetPageStatsAsync(employeeId, cancellationToken);
        await Task.WhenAll(candidatesTask, statsTask);

        var list = (await candidatesTask).ToList();
        var stats = await statsTask;
        _voteService.ApplyVoteCounts(list, stats.VoteCounts);
        ApplyNameBasedVoteCounts(list, stats.VoteCountsByName);

        var model = new VoteResultViewModel
        {
            Title = _configuration["Voting:Title"] ?? "投票系統",
            Candidates = list.OrderByDescending(c => c.VoteCount).ToList(),
            TotalVotes = stats.TotalVotes,
            IsUsingDemoData = !IsSpreadsheetConfigured(),
            IsConnectionSuccess = _candidateService.IsUsingLiveData,
            ConnectionMessage = _candidateService.ConnectionMessage,
            ConnectionHint = _candidateService.ConnectionHint,
            SignedInEmployeeId = employeeId,
            SignedInEmployeeName = _authService.GetSignedInEmployeeName(),
            HasVoted = stats.HasVoted
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Diagnostic(CancellationToken cancellationToken)
    {
        var result = await _candidateService.TestConnectionAsync(cancellationToken);
        return View(result);
    }

    private IActionResult? RequireLogin()
    {
        if (_authService.IsSignedIn())
        {
            return null;
        }

        return RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
    }

    private bool IsSpreadsheetConfigured()
    {
        var id = _configuration["GoogleSheets:SpreadsheetId"];
        return !string.IsNullOrWhiteSpace(id) && id != "YOUR_SPREADSHEET_ID";
    }

    private static void ApplyNameBasedVoteCounts(
        IList<Candidate> candidates,
        IReadOnlyDictionary<string, int> voteCountsByName)
    {
        if (voteCountsByName.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.VoteCount > 0)
            {
                continue;
            }

            var key = voteCountsByName.Keys.FirstOrDefault(name =>
                string.Equals(name, candidate.Name, StringComparison.OrdinalIgnoreCase));
            if (key is not null)
            {
                candidate.VoteCount = voteCountsByName[key];
            }
        }
    }
}
