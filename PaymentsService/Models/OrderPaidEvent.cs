namespace PaymentsService.Models;

public class OrderPaidEvent
{
    public Guid OrderId { get; set; }
    public string UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredOn { get; set; }
}