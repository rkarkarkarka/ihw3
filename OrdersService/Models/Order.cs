using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OrdersService.Models;

public class Order
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    [Display(Name = "NEW")]
    NEW = 0,
    
    [Display(Name = "FINISHED")]
    FINISHED = 1,
    
    [Display(Name = "CANCELLED")]
    CANCELLED = 2,
}