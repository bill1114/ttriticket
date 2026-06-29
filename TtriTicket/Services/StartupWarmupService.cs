namespace TtriTicket.Services;

public class StartupWarmupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupWarmupService> _logger;

    public StartupWarmupService(IServiceProvider serviceProvider, ILogger<StartupWarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var candidates = scope.ServiceProvider.GetRequiredService<ICandidateService>();
            await candidates.GetCandidatesAsync(cancellationToken: cancellationToken);

            var votes = scope.ServiceProvider.GetRequiredService<IVoteService>();
            await votes.GetTotalVotesAsync(cancellationToken);

            _logger.LogInformation("啟動預熱完成（候選人 + 投票紀錄快取）");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "啟動預熱失敗，將於首次請求時再載入");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
