namespace OrdersService.Models;

public class OrderStatusChangedEvent
{
    public Guid OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public DateTime OccurredOn { get; set; }
}