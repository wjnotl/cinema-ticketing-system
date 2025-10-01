namespace Supershow.BackgroundWorkers;

public class ExpiryCleanupBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _services;

    public ExpiryCleanupBackgroundWorker(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _services.CreateScope())
            {
                var cleanupService = scope.ServiceProvider.GetRequiredService<ExpiryCleanupService>();
                await cleanupService.Cleanup();
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // run every 1 minute
        }
    }
}