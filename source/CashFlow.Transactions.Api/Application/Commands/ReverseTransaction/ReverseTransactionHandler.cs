using System.Text.Json;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Persistence;
using MediatR;

namespace CashFlow.Transactions.Api.Application.Commands.ReverseTransaction;

internal sealed class ReverseTransactionHandler(
    ITransactionRepository repository,
    TransactionDbContext db) : IRequestHandler<ReverseTransactionCommand, ReverseTransactionResult>
{
    public async Task<ReverseTransactionResult> Handle(
        ReverseTransactionCommand command, CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(command.TransactionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Transaction {command.TransactionId} not found.");

        if (transaction.MerchantId != command.MerchantId)
            throw new UnauthorizedAccessException("Transaction does not belong to this merchant.");

        var reversalResult = transaction.Reverse(command.Reason);
        if (reversalResult.IsFailure)
            throw new InvalidOperationException(reversalResult.Error);

        var reversal = reversalResult.Value;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.Transactions.AddAsync(reversal, cancellationToken);
        db.Transactions.Update(transaction);

        foreach (var domainEvent in reversal.DomainEvents.OfType<Domain.Events.TransactionReversedDomainEvent>())
        {
            var integrationEvent = new TransactionReversedEvent
            {
                EventId = domainEvent.EventId,
                OccurredAt = domainEvent.OccurredAt,
                Data = new TransactionReversedData
                {
                    ReversalTransactionId = domainEvent.ReversalTransactionId,
                    OriginalTransactionId = domainEvent.OriginalTransactionId,
                    MerchantId = domainEvent.MerchantId,
                    OriginalType = domainEvent.OriginalType.ToString(),
                    Amount = domainEvent.Amount,
                    Currency = domainEvent.Currency,
                    OriginalOccurredOn = domainEvent.OriginalOccurredOn,
                    Reason = domainEvent.Reason
                }
            };

            await db.OutboxMessages.AddAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                AggregateType = "Transaction",
                AggregateId = reversal.Id,
                EventType = integrationEvent.EventType,
                EventVersion = integrationEvent.EventVersion,
                Payload = JsonSerializer.Serialize(integrationEvent),
                OccurredAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        reversal.ClearDomainEvents();

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new ReverseTransactionResult(reversal.Id, transaction.Id);
    }
}
