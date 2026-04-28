using CashFlow.Transactions.Domain.ValueObjects;
using FluentAssertions;

namespace CashFlow.Transactions.UnitTests.Domain;

public sealed class AmountValueObjectTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var result = Amount.Create(100.50m, "BRL");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(100.50m);
        result.Value.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Create_WithZero_ShouldFail()
    {
        var result = Amount.Create(0m);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNegative_ShouldFail()
    {
        var result = Amount.Create(-1m);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultCurrency_ShouldBeBRL()
    {
        var result = Amount.Create(50m);
        result.Value.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Create_ShouldNormalizeCurrencyToUpperCase()
    {
        var result = Amount.Create(50m, "usd");
        result.Value.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithInvalidCurrencyLength_ShouldFail()
    {
        var result = Amount.Create(50m, "USDD");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void TwoAmountsWithSameValues_ShouldBeEqual()
    {
        var a1 = Amount.Create(100m, "BRL").Value;
        var a2 = Amount.Create(100m, "BRL").Value;
        a1.Should().Be(a2);
    }
}
