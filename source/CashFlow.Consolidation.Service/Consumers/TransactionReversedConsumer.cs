using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Cache;
using CashFlow.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CashFlow.Consolidation.Service.Consumers;

public sealed class TransactionReversedConsumer(
    IDailyBalanceRepository repository,
    ILogger<TransactionReversedConsumer> logger,
    DailyBalanceCache? cache = null)
    : IConsumer<TransactionReversedEvent>
{
    public async Task Consume(ConsumeContext<TransactionReversedEvent> context)
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
        var date = DateOnly.FromDateTime(data.OriginalOccurredOn.UtcDateTime);

        var wasCredit = data.OriginalType.Equals("Credit", StringComparison.OrdinalIgnoreCase);
        var creditsDelta = wasCredit ? -data.Amount : data.Amount;
        var debitsDelta = wasCredit ? data.Amount : -data.Amount;

        await repository.UpsertAsync(data.MerchantId, date, creditsDelta, debitsDelta, 1, ct);
        await repository.MarkEventProcessedAsync(eventId, msg.EventType, msg.OccurredAt, ct);

        if (cache is not null)
            await cache.IncrementAsync(data.MerchantId, date, creditsDelta, debitsDelta, 1);

        logger.LogInformation("Processed TransactionReversed {EventId} for merchant {MerchantId} date {Date}",
            eventId, data.MerchantId, date);
    }
}
