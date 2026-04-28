using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Events;
using CashFlow.Transactions.Domain.Primitives;
using CashFlow.Transactions.Domain.ValueObjects;

namespace CashFlow.Transactions.Domain.Aggregates;

public sealed class Transaction : Entity
{
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    private Transaction(
        Guid id,
        Guid merchantId,
        TransactionType type,
        Amount amount,
        DateTimeOffset occurredOn,
        string? description,
        Guid? reversalOf = null)
        : base(id)
    {
        MerchantId = merchantId;
        Type = type;
        Amount = amount;
        OccurredOn = occurredOn;
        Description = description;
        Status = TransactionStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        ReversalOf = reversalOf;
    }

    private Transaction() : base(Guid.Empty) { }

    public Guid MerchantId { get; private set; }
    public TransactionType Type { get; private set; }
    public Amount Amount { get; private set; } = null!;
    public DateTimeOffset OccurredOn { get; private set; }
    public string? Description { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ReversalOf { get; private set; }

    public static Result<Transaction> Create(
        Guid merchantId,
        TransactionType type,
        decimal amountValue,
        string currency,
        DateTimeOffset occurredOn,
        string? description)
    {
        if (merchantId == Guid.Empty)
            return Result<Transaction>.Failure("MerchantId is required.");

        if (!Enum.IsDefined(typeof(TransactionType), type))
            return Result<Transaction>.Failure("Invalid transaction type.");

        var amountResult = Amount.Create(amountValue, currency);
        if (amountResult.IsFailure)
            return Result<Transaction>.Failure(amountResult.Error);

        if (occurredOn > DateTimeOffset.UtcNow.Add(FutureTolerance))
            return Result<Transaction>.Failure("OccurredOn cannot be more than 5 minutes in the future.");

        if (description?.Length > 140)
            return Result<Transaction>.Failure("Description cannot exceed 140 characters.");

        var transaction = new Transaction(
            Guid.NewGuid(),
            merchantId,
            type,
            amountResult.Value,
            occurredOn,
            description);

        transaction.RaiseDomainEvent(new TransactionCreatedDomainEvent
        {
            TransactionId = transaction.Id,
            MerchantId = merchantId,
            Type = type,
            Amount = amountValue,
            Currency = amountResult.Value.Currency,
            TransactionOccurredOn = occurredOn,
            Description = description
        });

        return Result<Transaction>.Success(transaction);
    }

    public Result<Transaction> Reverse(string? reason = null)
    {
        if (Status == TransactionStatus.Reversed)
            return Result<Transaction>.Failure("Transaction has already been reversed.");

        var oppositeType = Type == TransactionType.Credit ? TransactionType.Debit : TransactionType.Credit;

        var reversal = new Transaction(
            Guid.NewGuid(),
            MerchantId,
            oppositeType,
            Amount,
            OccurredOn,
            $"Reversal of {Id}",
            reversalOf: Id);

        Status = TransactionStatus.Reversed;

        reversal.RaiseDomainEvent(new TransactionReversedDomainEvent
        {
            ReversalTransactionId = reversal.Id,
            OriginalTransactionId = Id,
            MerchantId = MerchantId,
            OriginalType = Type,
            Amount = Amount.Value,
            Currency = Amount.Currency,
            OriginalOccurredOn = OccurredOn,
            Reason = reason
        });

        return Result<Transaction>.Success(reversal);
    }
}
