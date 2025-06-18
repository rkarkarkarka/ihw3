namespace PaymentsService.Services;

public class InboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxBackgroundService> _logger;

    public InboxBackgroundService(IServiceProvider serviceProvider, ILogger<InboxBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IInboxProcessor>();
                
                var processedCount = await processor.ProcessInboxMessagesAsync(stoppingToken);
                
                if (processedCount > 0)
                {
                    _logger.LogInformation("Processed {Count} inbox messages", processedCount);
                }

                await Task.Delay(5000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inbox messages");
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}