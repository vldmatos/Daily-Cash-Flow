using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Cache;
using CashFlow.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CashFlow.Consolidation.Service.Consumers;

public sealed class TransactionCreatedConsumer(
    IDailyBalanceRepository repository,
    ILogger<TransactionCreatedConsumer> logger,
    DailyBalanceCache? cache = null)
    : IConsumer<TransactionCreatedEvent>
{
    public async Task Consume(ConsumeContext<TransactionCreatedEvent> context)
    {
        var msg = context.Message;
        var eventId = msg.EventId;
        var ct = context.CancellationToken;

        if (await repository.IsEventProcessedAsync(eventId, ct))
        {
            logger.LogDebug("Event {EventId} already processed, skipping.", eventId);
            return;
        }

        var data = msg.Data;
        var date = DateOnly.FromDateTime(data.OccurredOn.UtcDateTime);
        var isCredit = data.Type.Equals("Credit", StringComparison.OrdinalIgnoreCase);

        var creditsDelta = isCredit ? data.Amount : 0m;
        var debitsDelta = isCredit ? 0m : data.Amount;

        await repository.UpsertAsync(data.MerchantId, date, creditsDelta, debitsDelta, 1, ct);
        await repository.MarkEventProcessedAsync(eventId, msg.EventType, msg.OccurredAt, ct);

        if (cache is not null)
            await cache.IncrementAsync(data.MerchantId, date, creditsDelta, debitsDelta, 1);

        logger.LogInformation("Processed TransactionCreated {EventId} for merchant {MerchantId} date {Date}",
            eventId, data.MerchantId, date);
    }
}
