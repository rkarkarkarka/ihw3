namespace OrdersService.Models;

public class PaymentProcessedEvent
{
    public Guid OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime OccurredOn { get; set; }
}