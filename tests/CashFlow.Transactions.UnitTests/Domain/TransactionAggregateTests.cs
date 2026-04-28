using CashFlow.Transactions.Domain.Aggregates;
using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Events;
using FluentAssertions;

namespace CashFlow.Transactions.UnitTests.Domain;

public sealed class TransactionAggregateTests
{
    private static readonly Guid MerchantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var result = Transaction.Create(
            MerchantId, TransactionType.Credit, 150.00m, "BRL",
            DateTimeOffset.UtcNow.AddMinutes(-1), "Venda PDV #1");

        result.IsSuccess.Should().BeTrue();
        result.Value.MerchantId.Should().Be(MerchantId);
        result.Value.Type.Should().Be(TransactionType.Credit);
        result.Value.Amount.Value.Should().Be(150.00m);
        result.Value.Status.Should().Be(TransactionStatus.Active);
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldFail()
    {
        var result = Transaction.Create(
            MerchantId, TransactionType.Credit, 0m, "BRL", DateTimeOffset.UtcNow, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("greater than zero");
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldFail()
    {
        var result = Transaction.Create(
            MerchantId, TransactionType.Debit, -50m, "BRL", DateTimeOffset.UtcNow, null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithFutureDateBeyondTolerance_ShouldFail()
    {
        var result = Transaction.Create(
            MerchantId, TransactionType.Credit, 100m, "BRL",
            DateTimeOffset.UtcNow.AddMinutes(10), null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("future");
    }

    [Fact]
    public void Create_WithDescriptionExceeding140Chars_ShouldFail()
    {
        var longDesc = new string('x', 141);
        var result = Transaction.Create(
            MerchantId, TransactionType.Credit, 100m, "BRL", DateTimeOffset.UtcNow, longDesc);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("140");
    }

    [Fact]
    public void Create_WithEmptyMerchantId_ShouldFail()
    {
        var result = Transaction.Create(
            Guid.Empty, TransactionType.Credit, 100m, "BRL", DateTimeOffset.UtcNow, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MerchantId");
    }

    [Fact]
    public void Create_ShouldRaiseTransactionCreatedDomainEvent()
    {
        var result = Transaction.Create(
            MerchantId, TransactionType.Credit, 100m, "BRL", DateTimeOffset.UtcNow, "test");

        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TransactionCreatedDomainEvent>();
    }

    [Fact]
    public void Reverse_ShouldCreateOppositeTypeReversal()
    {
        var txResult = Transaction.Create(
            MerchantId, TransactionType.Credit, 100m, "BRL", DateTimeOffset.UtcNow, null);
        var tx = txResult.Value;

        var reversalResult = tx.Reverse("test reason");

        reversalResult.IsSuccess.Should().BeTrue();
        reversalResult.Value.Type.Should().Be(TransactionType.Debit);
        reversalResult.Value.Amount.Value.Should().Be(100m);
        tx.Status.Should().Be(TransactionStatus.Reversed);
    }

    [Fact]
    public void Reverse_AlreadyReversed_ShouldFail()
    {
        var txResult = Transaction.Create(
            MerchantId, TransactionType.Credit, 100m, "BRL", DateTimeOffset.UtcNow, null);
        var tx = txResult.Value;
        tx.Reverse();

        var secondReversal = tx.Reverse();

        secondReversal.IsFailure.Should().BeTrue();
        secondReversal.Error.Should().Contain("already been reversed");
    }

    [Fact]
    public void Reverse_ShouldRaiseTransactionReversedDomainEvent()
    {
        var txResult = Transaction.Create(
            MerchantId, TransactionType.Credit, 100m, "BRL", DateTimeOffset.UtcNow, null);
        var reversal = txResult.Value.Reverse().Value;

        reversal.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TransactionReversedDomainEvent>();
    }

    [Theory]
    [InlineData(100.00, "BRL")]
    [InlineData(0.0001, "USD")]
    [InlineData(999999.9999, "EUR")]
    public void Create_ValidAmounts_ShouldSucceed(decimal amount, string currency)
    {
        var result = Transaction.Create(
            MerchantId, TransactionType.Credit, amount, currency, DateTimeOffset.UtcNow, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Value.Should().Be(amount);
        result.Value.Amount.Currency.Should().Be(currency);
    }

    [Fact]
    public void Create_WithinFutureTolerance_ShouldSucceed()
    {
        var result = Transaction.Create(
            MerchantId, TransactionType.Credit, 100m, "BRL",
            DateTimeOffset.UtcNow.AddMinutes(4), null);

        result.IsSuccess.Should().BeTrue();
    }
}
