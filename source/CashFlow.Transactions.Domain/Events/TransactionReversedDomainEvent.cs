using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Primitives;

namespace CashFlow.Transactions.Domain.Events;

public sealed record TransactionReversedDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public required Guid ReversalTransactionId { get; init; }
    public required Guid OriginalTransactionId { get; init; }
    public required Guid MerchantId { get; init; }
    public required TransactionType OriginalType { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset OriginalOccurredOn { get; init; }
    public string? Reason { get; init; }
}
