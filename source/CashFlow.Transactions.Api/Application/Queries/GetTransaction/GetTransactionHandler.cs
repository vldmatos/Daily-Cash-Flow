using CashFlow.Transactions.Domain.Repositories;
using MediatR;

namespace CashFlow.Transactions.Api.Application.Queries.GetTransaction;

internal sealed class GetTransactionHandler(ITransactionRepository repository)
    : IRequestHandler<GetTransactionQuery, GetTransactionResult?>
{
    public async Task<GetTransactionResult?> Handle(
        GetTransactionQuery query, CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(query.TransactionId, cancellationToken);

        if (transaction is null) return null;

        if (transaction.MerchantId != query.MerchantId)
            throw new UnauthorizedAccessException("Transaction does not belong to this merchant.");

        return new GetTransactionResult(
            transaction.Id,
            transaction.MerchantId,
            transaction.Type.ToString(),
            transaction.Amount.Value,
            transaction.Amount.Currency,
            transaction.OccurredOn,
            transaction.Description,
            transaction.Status.ToString(),
            transaction.CreatedAt,
            transaction.ReversalOf);
    }
}
