namespace PaymentsService.Models;

public class BalanceUpdatedEvent
{
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal NewBalance { get; set; }
    public DateTime OccurredOn { get; set; }
}
