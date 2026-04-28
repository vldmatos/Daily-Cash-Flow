namespace CashFlow.Shared.Contracts.Events;

public sealed record TransactionCreatedEvent
{
    public required Guid EventId { get; init; }
    public string EventType { get; init; } = "TransactionCreated";
    public int EventVersion { get; init; } = 1;
    public required DateTimeOffset OccurredAt { get; init; }
    public required TransactionCreatedData Data { get; init; }
}

public sealed record TransactionCreatedData
{
    public required Guid TransactionId { get; init; }
    public required Guid MerchantId { get; init; }
    public required string Type { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
    public string? Description { get; init; }
}
