namespace OrdersService.Data;

public class InboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredOn { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsProcessed => ProcessedOn.HasValue;
}