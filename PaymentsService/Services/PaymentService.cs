using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Models;
using System.Text.Json;

namespace PaymentsService.Services;

public interface IPaymentService
{
    Task<Account> CreateAccountAsync(string userId, decimal initialBalance = 0);
    Task<decimal?> GetBalanceAsync(string userId);
    Task DepositAsync(string userId, decimal amount);
    Task ProcessOrderPaymentAsync(Guid orderId, string userId, decimal amount);
    Task<Account?> GetAccountByUserIdAsync(string userId);
}

public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(PaymentDbContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Account?> GetAccountByUserIdAsync(string userId)
    {
        return await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
    }

    public async Task<Account> CreateAccountAsync(string userId, decimal initialBalance = 0)
    {
        var existingAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        if (existingAccount != null)
        {
            throw new InvalidOperationException("Account already exists for this user");
        }

        var account = new Account
        {
            UserId = userId,
            Balance = initialBalance,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Account created for user {UserId} with balance {Balance}", userId, initialBalance);
        return account;
    }

    public async Task<decimal?> GetBalanceAsync(string userId)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        return account?.Balance;
    }

    public async Task DepositAsync(string userId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
            if (account == null)
                throw new InvalidOperationException("Account not found");

            account.Balance += amount;
            account.UpdatedAt = DateTime.UtcNow;
            account.Version++;

            var paymentTransaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrderId = Guid.Empty,
                Amount = amount,
                Type = TransactionType.CREDIT,
                Status = TransactionStatus.COMPLETED,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentTransactions.Add(paymentTransaction);
            
            await CreateBalanceUpdatedEventAsync(userId, amount, account.Balance);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Deposited {Amount} to user {UserId}, new balance: {Balance}", 
                amount, userId, account.Balance);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task ProcessOrderPaymentAsync(Guid orderId, string userId, decimal amount)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var existingTransaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.OrderId == orderId && t.Type == TransactionType.DEBIT);

            if (existingTransaction != null)
            {
                _logger.LogInformation("Order {OrderId} already processed, skipping", orderId);
                return;
            }

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
            if (account == null)
            {
                _logger.LogWarning("Account not found for user {UserId}, order {OrderId}", userId, orderId);
                await CreatePaymentResultEventAsync(orderId, userId, amount, false, "Account not found");
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }

            if (account.Balance < amount)
            {
                _logger.LogWarning("Insufficient funds for user {UserId}, order {OrderId}. Required: {Amount}, Available: {Balance}", 
                    userId, orderId, amount, account.Balance);
                await CreatePaymentResultEventAsync(orderId, userId, amount, false, "Insufficient funds");
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }
            
            account.Balance -= amount;
            account.UpdatedAt = DateTime.UtcNow;
            account.Version++;

            var paymentTransaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrderId = orderId,
                Amount = amount,
                Type = TransactionType.DEBIT,
                Status = TransactionStatus.COMPLETED,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentTransactions.Add(paymentTransaction);
            
            await CreatePaymentResultEventAsync(orderId, userId, amount, true, null);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Payment processed successfully for order {OrderId}, user {UserId}, amount {Amount}, new balance: {Balance}", 
                orderId, userId, amount, account.Balance);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Payment processing failed for order {OrderId}, user {UserId}", orderId, userId);
            await CreatePaymentResultEventAsync(orderId, userId, amount, false, ex.Message);
            throw;
        }
    }

    private async Task CreatePaymentResultEventAsync(Guid orderId, string userId, decimal amount, bool success, string? errorMessage)
    {
        var paymentEvent = new PaymentProcessedEvent
        {
            OrderId = orderId,
            UserId = userId,
            Amount = amount,
            Success = success,
            ErrorMessage = errorMessage,
            OccurredOn = DateTime.UtcNow
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(PaymentProcessedEvent),
            Content = JsonSerializer.Serialize(paymentEvent),
            OccurredOn = DateTime.UtcNow
        };

        _context.OutboxMessages.Add(outboxMessage);
        await Task.CompletedTask;
    }

    private async Task CreateBalanceUpdatedEventAsync(string userId, decimal amount, decimal newBalance)
    {
        var balanceEvent = new BalanceUpdatedEvent
        {
            UserId = userId,
            Amount = amount,
            NewBalance = newBalance,
            OccurredOn = DateTime.UtcNow
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(BalanceUpdatedEvent),
            Content = JsonSerializer.Serialize(balanceEvent),
            OccurredOn = DateTime.UtcNow
        };

        _context.OutboxMessages.Add(outboxMessage);
        await Task.CompletedTask;
    }
}
