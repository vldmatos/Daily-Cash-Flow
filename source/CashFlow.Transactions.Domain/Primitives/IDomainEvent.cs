using MediatR;

namespace CashFlow.Transactions.Domain.Primitives;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
