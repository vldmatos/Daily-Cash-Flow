namespace CashFlow.Transactions.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public required string AggregateType { get; set; }
    public Guid AggregateId { get; set; }
    public required string EventType { get; set; }
    public int EventVersion { get; set; } = 1;
    public required string Payload { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int RetryCount { get; set; }
}
