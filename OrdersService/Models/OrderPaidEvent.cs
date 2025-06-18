namespace OrdersService.Models;

public class OrderPaidEvent
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredOn { get; set; }
}