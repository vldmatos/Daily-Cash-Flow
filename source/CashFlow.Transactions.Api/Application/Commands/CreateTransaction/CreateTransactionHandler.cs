using System.Text.Json;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Domain.Aggregates;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.Api.Application.Commands.CreateTransaction;

internal sealed class CreateTransactionHandler(
    ITransactionRepository repository,
    TransactionDbContext db) : IRequestHandler<CreateTransactionCommand, CreateTransactionResult>
{
    public async Task<CreateTransactionResult> Handle(
        CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        if (command.IdempotencyKey is not null)
        {
            var cached = await repository.GetIdempotencyResponseAsync(
                command.MerchantId, command.IdempotencyKey, cancellationToken);

            if (cached is not null)
                return JsonSerializer.Deserialize<CreateTransactionResult>(cached)!;
        }

        var transactionResult = Transaction.Create(
            command.MerchantId,
            command.Type,
            command.Amount,
            command.Currency,
            command.OccurredOn,
            command.Description);

        if (transactionResult.IsFailure)
            throw new InvalidOperationException(transactionResult.Error);

        var transaction = transactionResult.Value;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.Transactions.AddAsync(transaction, cancellationToken);

        foreach (var domainEvent in transaction.DomainEvents)
        {
            var integration = MapToIntegrationEvent(domainEvent, transaction);
            if (integration is null) continue;

            await db.OutboxMessages.AddAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                AggregateType = "Transaction",
                AggregateId = transaction.Id,
                EventType = integration.EventType,
                EventVersion = integration.EventVersion,
                Payload = JsonSerializer.Serialize(integration, integration.GetType()),
                OccurredAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        transaction.ClearDomainEvents();

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var result = new CreateTransactionResult(
            transaction.Id,
            "Accepted",
            transaction.CreatedAt);

        if (command.IdempotencyKey is not null)
        {
            await repository.SaveIdempotencyKeyAsync(
                command.MerchantId,
                command.IdempotencyKey,
                JsonSerializer.Serialize(result),
                cancellationToken);
        }

        return result;
    }

    private static dynamic? MapToIntegrationEvent(
        Domain.Primitives.IDomainEvent domainEvent, Transaction transaction)
    {
        if (domainEvent is Domain.Events.TransactionCreatedDomainEvent created)
        {
            return new TransactionCreatedEvent
            {
                EventId = created.EventId,
                OccurredAt = created.OccurredAt,
                Data = new TransactionCreatedData
                {
                    TransactionId = transaction.Id,
                    MerchantId = created.MerchantId,
                    Type = created.Type.ToString(),
                    Amount = created.Amount,
                    Currency = created.Currency,
                    OccurredOn = created.TransactionOccurredOn,
                    Description = created.Description
                }
            };
        }
        return null;
    }
}
