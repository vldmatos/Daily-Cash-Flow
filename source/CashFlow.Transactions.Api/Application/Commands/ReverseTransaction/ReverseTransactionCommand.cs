using MediatR;

namespace CashFlow.Transactions.Api.Application.Commands.ReverseTransaction;

public sealed record ReverseTransactionCommand : IRequest<ReverseTransactionResult>
{
    public required Guid TransactionId { get; init; }
    public required Guid MerchantId { get; init; }
    public string? Reason { get; init; }
    public string? IdempotencyKey { get; init; }
}

public sealed record ReverseTransactionResult(Guid ReversalId, Guid OriginalId);
