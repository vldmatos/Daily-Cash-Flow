using System.Text.Json;
using CashFlow.Shared.Contracts.Events;
using CashFlow.Transactions.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CashFlow.Transactions.Infrastructure.Outbox;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in OutboxPublisher loop.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await db.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(50)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                await PublishMessageAsync(bus, message, ct);
                message.PublishedAt = DateTimeOffset.UtcNow;
                logger.LogDebug("Published outbox message {Id} type {EventType}", message.Id, message.EventType);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                logger.LogWarning(ex, "Failed to publish outbox message {Id}, retry {Retry}",
                    message.Id, message.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task PublishMessageAsync(IPublishEndpoint bus, OutboxMessage message, CancellationToken ct)
    {
        switch (message.EventType)
        {
            case "TransactionCreated":
                var created = JsonSerializer.Deserialize<TransactionCreatedEvent>(message.Payload)!;
                await bus.Publish(created, ct);
                break;

            case "TransactionReversed":
                var reversed = JsonSerializer.Deserialize<TransactionReversedEvent>(message.Payload)!;
                await bus.Publish(reversed, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown event type: {message.EventType}");
        }
    }
}
