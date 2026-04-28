namespace CashFlow.Shared.Contracts.Events;

public sealed record TransactionReversedEvent
{
    public required Guid EventId { get; init; }
    public string EventType { get; init; } = "TransactionReversed";
    public int EventVersion { get; init; } = 1;
    public required DateTimeOffset OccurredAt { get; init; }
    public required TransactionReversedData Data { get; init; }
}

public sealed record TransactionReversedData
{
    public required Guid ReversalTransactionId { get; init; }
    public required Guid OriginalTransactionId { get; init; }
    public required Guid MerchantId { get; init; }
    public required string OriginalType { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset OriginalOccurredOn { get; init; }
    public string? Reason { get; init; }
}
