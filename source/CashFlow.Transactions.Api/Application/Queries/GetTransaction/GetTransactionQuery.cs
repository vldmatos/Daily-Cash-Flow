using MediatR;

namespace CashFlow.Transactions.Api.Application.Queries.GetTransaction;

public sealed record GetTransactionQuery(Guid TransactionId, Guid MerchantId)
    : IRequest<GetTransactionResult?>;

public sealed record GetTransactionResult(
    Guid Id,
    Guid MerchantId,
    string Type,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredOn,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? ReversalOf);
