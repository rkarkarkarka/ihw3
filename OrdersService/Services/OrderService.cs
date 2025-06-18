using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Models;
using System.Text.Json;

namespace OrdersService.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(string userId, decimal amount, string description);
    Task<List<Order>> GetOrdersByUserIdAsync(string userId);
    Task<Order?> GetOrderByIdAsync(Guid orderId);
    Task ProcessPaymentResultAsync(Guid orderId, bool paymentSuccessful, string? errorMessage = null);
    Task CheckPendingOrdersForUserAsync(string userId, decimal currentBalance);
}

public class OrderService : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(string userId, decimal amount, string description)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = amount,
                Description = description,
                Status = OrderStatus.NEW,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            
            var orderCreatedEvent = new OrderCreatedEvent
            {
                OrderId = order.Id,
                UserId = order.UserId,
                Amount = order.Amount,
                Description = order.Description,
                OccurredOn = DateTime.UtcNow
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(OrderCreatedEvent),
                Content = JsonSerializer.Serialize(orderCreatedEvent),
                OccurredOn = DateTime.UtcNow
            };

            _context.OutboxMessages.Add(outboxMessage);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Order created: {OrderId} for user {UserId} with amount {Amount}", 
                order.Id, userId, amount);

            return order;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Order>> GetOrdersByUserIdAsync(string userId)
    {
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        return await _context.Orders.FindAsync(orderId);
    }

    public async Task ProcessPaymentResultAsync(Guid orderId, bool paymentSuccessful, string? errorMessage = null)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for payment result processing: {OrderId}", orderId);
                return;
            }

            var newStatus = paymentSuccessful ? OrderStatus.FINISHED : OrderStatus.CANCELLED;
            await UpdateOrderStatusInternalAsync(order, newStatus, transaction);

            _logger.LogInformation("Payment result processed for order {OrderId}: {Success}, new status: {Status}", 
                orderId, paymentSuccessful, newStatus);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task CheckPendingOrdersForUserAsync(string userId, decimal currentBalance)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var pendingOrders = await _context.Orders
                .Where(o => o.UserId == userId && o.Status == OrderStatus.NEW)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            decimal remainingBalance = currentBalance;

            foreach (var order in pendingOrders)
            {
                if (remainingBalance >= order.Amount)
                {
                    remainingBalance -= order.Amount;
                    await UpdateOrderStatusInternalAsync(order, OrderStatus.FINISHED, transaction);
                    
                    _logger.LogInformation("Order {OrderId} automatically completed due to sufficient balance. Amount: {Amount}, Remaining balance: {Balance}", 
                        order.Id, order.Amount, remainingBalance);
                }
                else
                {
                    await UpdateOrderStatusInternalAsync(order, OrderStatus.CANCELLED, transaction);
                    
                    _logger.LogInformation("Order {OrderId} automatically cancelled due to insufficient balance. Required: {Amount}, Available: {Balance}", 
                        order.Id, order.Amount, remainingBalance);
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task UpdateOrderStatusInternalAsync(Order order, OrderStatus newStatus, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        var previousStatus = order.Status;
        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;
        
        var statusChangedEvent = new OrderStatusChangedEvent
        {
            OrderId = order.Id,
            UserId = order.UserId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            OccurredOn = DateTime.UtcNow
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(OrderStatusChangedEvent),
            Content = JsonSerializer.Serialize(statusChangedEvent),
            OccurredOn = DateTime.UtcNow
        };

        _context.OutboxMessages.Add(outboxMessage);
        await _context.SaveChangesAsync();
    }
}
