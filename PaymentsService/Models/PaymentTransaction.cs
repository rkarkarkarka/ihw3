using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PaymentsService.Models;

public class PaymentTransaction
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransactionType
{
    [Display(Name = "DEBIT")]
    DEBIT,
    [Display(Name = "CREDIT")]
    CREDIT
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransactionStatus
{
    [Display(Name = "NEW")]
    NEW = 0,
    [Display(Name = "COMPLETED")]
    COMPLETED = 1,
    [Display(Name = "CANCELLED")]
    CANCELLED = 2,
}