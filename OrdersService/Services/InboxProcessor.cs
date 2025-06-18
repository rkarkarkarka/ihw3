using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Models;
using System.Text.Json;

namespace OrdersService.Services;

public interface IInboxProcessor
{
    Task<int> ProcessInboxMessagesAsync(CancellationToken cancellationToken = default);
}

public class InboxProcessor : IInboxProcessor
{
    private readonly OrderDbContext _context;
    private readonly IOrderService _orderService;
    private readonly ILogger<InboxProcessor> _logger;
    private const int BatchSize = 10;

    public InboxProcessor(OrderDbContext context, IOrderService orderService, ILogger<InboxProcessor> logger)
    {
        _context = context;
        _orderService = orderService;
        _logger = logger;
    }

    public async Task<int> ProcessInboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _context.InboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (!messages.Any())
            return 0;

        int processedCount = 0;

        foreach (var message in messages)
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken);
                processedCount++;
                _logger.LogInformation("Successfully processed inbox message {MessageId} of type {MessageType}", 
                    message.Id, message.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process inbox message {MessageId} of type {MessageType}", 
                    message.Id, message.Type);
                
                message.ErrorMessage = ex.Message;
                message.RetryCount++;

                if (message.RetryCount >= 3)
                {
                    message.ProcessedOn = DateTime.UtcNow;
                    _logger.LogWarning("Inbox message {MessageId} exceeded retry limit, marking as processed", message.Id);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return processedCount;
    }

    private async Task ProcessMessageAsync(InboxMessage message, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case nameof(PaymentProcessedEvent):
                await ProcessPaymentResultEventAsync(message, cancellationToken);
                break;
            case nameof(BalanceUpdatedEvent):
                await ProcessBalanceUpdatedEventAsync(message, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Message type {message.Type} is not supported");
        }
    }

    private async Task ProcessPaymentResultEventAsync(InboxMessage message, CancellationToken cancellationToken)
    {
        var paymentEvent = JsonSerializer.Deserialize<PaymentProcessedEvent>(message.Content);
        if (paymentEvent != null)
        {
            await _orderService.ProcessPaymentResultAsync(
                paymentEvent.OrderId,
                paymentEvent.Success,
                paymentEvent.ErrorMessage);

            message.ProcessedOn = DateTime.UtcNow;
        }
    }

    private async Task ProcessBalanceUpdatedEventAsync(InboxMessage message, CancellationToken cancellationToken)
    {
        var balanceEvent = JsonSerializer.Deserialize<BalanceUpdatedEvent>(message.Content);
        if (balanceEvent != null)
        {
            await _orderService.CheckPendingOrdersForUserAsync(balanceEvent.UserId, balanceEvent.NewBalance);
            message.ProcessedOn = DateTime.UtcNow;
        }
    }
}
