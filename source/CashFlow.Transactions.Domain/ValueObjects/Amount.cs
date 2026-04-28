using CashFlow.Transactions.Domain.Primitives;

namespace CashFlow.Transactions.Domain.ValueObjects;

public sealed record Amount
{
    public decimal Value { get; }
    public string Currency { get; }

    private Amount(decimal value, string currency)
    {
        Value = value;
        Currency = currency;
    }

    public static Result<Amount> Create(decimal value, string currency = "BRL")
    {
        if (value <= 0)
            return Result<Amount>.Failure("Amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return Result<Amount>.Failure("Currency must be a valid 3-letter ISO code.");

        return Result<Amount>.Success(new Amount(value, currency.ToUpperInvariant()));
    }

    public override string ToString() => $"{Value:F4} {Currency}";
}
