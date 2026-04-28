using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Service.Consumers;
using CashFlow.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FluentAssertions;

namespace CashFlow.Consolidation.UnitTests.Consumers;

public sealed class TransactionCreatedConsumerTests
{
    private readonly IDailyBalanceRepository _repository = Substitute.For<IDailyBalanceRepository>();

    [Fact]
    public async Task Consume_NewEvent_ShouldUpsertBalance()
    {
        _repository.IsEventProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var consumer = new TransactionCreatedConsumer(
            _repository, NullLogger<TransactionCreatedConsumer>.Instance);

        var msg = CreateMessage("Credit", 100m);
        var ctx = CreateContext(msg);

        await consumer.Consume(ctx);

        await _repository.Received(1).UpsertAsync(
            msg.Data.MerchantId,
            DateOnly.FromDateTime(msg.Data.OccurredOn.UtcDateTime),
            100m, 0m, 1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_DebitEvent_ShouldUpsertWithDebitDelta()
    {
        _repository.IsEventProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var consumer = new TransactionCreatedConsumer(
            _repository, NullLogger<TransactionCreatedConsumer>.Instance);

        var msg = CreateMessage("Debit", 50m);
        var ctx = CreateContext(msg);

        await consumer.Consume(ctx);

        await _repository.Received(1).UpsertAsync(
            msg.Data.MerchantId,
            Arg.Any<DateOnly>(),
            0m, 50m, 1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_AlreadyProcessedEvent_ShouldSkip()
    {
        _repository.IsEventProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var consumer = new TransactionCreatedConsumer(
            _repository, NullLogger<TransactionCreatedConsumer>.Instance);

        await consumer.Consume(CreateContext(CreateMessage("Credit", 100m)));

        await _repository.DidNotReceive().UpsertAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(),
            Arg.Any<decimal>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private static TransactionCreatedEvent CreateMessage(string type, decimal amount) =>
        new()
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Data = new TransactionCreatedData
            {
                TransactionId = Guid.NewGuid(),
                MerchantId = Guid.NewGuid(),
                Type = type,
                Amount = amount,
                Currency = "BRL",
                OccurredOn = DateTimeOffset.UtcNow
            }
        };

    private static ConsumeContext<TransactionCreatedEvent> CreateContext(TransactionCreatedEvent msg)
    {
        var ctx = Substitute.For<ConsumeContext<TransactionCreatedEvent>>();
        ctx.Message.Returns(msg);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }
}
