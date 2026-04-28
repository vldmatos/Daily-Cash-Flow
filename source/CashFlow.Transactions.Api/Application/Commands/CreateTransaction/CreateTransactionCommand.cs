using CashFlow.Transactions.Domain.Enums;
using MediatR;

namespace CashFlow.Transactions.Api.Application.Commands.CreateTransaction;

public sealed record CreateTransactionCommand : IRequest<CreateTransactionResult>
{
    public required Guid MerchantId { get; init; }
    public required TransactionType Type { get; init; }
    public required decimal Amount { get; init; }
    public string Currency { get; init; } = "BRL";
    public required DateTimeOffset OccurredOn { get; init; }
    public string? Description { get; init; }
    public string? IdempotencyKey { get; init; }
}

public sealed record CreateTransactionResult(Guid Id, string Status, DateTimeOffset CreatedAt);
