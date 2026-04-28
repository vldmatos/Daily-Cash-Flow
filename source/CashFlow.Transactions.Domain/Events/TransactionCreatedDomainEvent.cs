using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Primitives;

namespace CashFlow.Transactions.Domain.Events;

public sealed record TransactionCreatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public required Guid TransactionId { get; init; }
    public required Guid MerchantId { get; init; }
    public required TransactionType Type { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset TransactionOccurredOn { get; init; }
    public string? Description { get; init; }
}
