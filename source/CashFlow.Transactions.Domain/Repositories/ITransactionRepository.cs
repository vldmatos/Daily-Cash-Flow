using CashFlow.Transactions.Domain.Aggregates;

namespace CashFlow.Transactions.Domain.Repositories;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<bool> ExistsIdempotencyKeyAsync(Guid merchantId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task SaveIdempotencyKeyAsync(Guid merchantId, string idempotencyKey, string responseJson, CancellationToken cancellationToken = default);
    Task<string?> GetIdempotencyResponseAsync(Guid merchantId, string idempotencyKey, CancellationToken cancellationToken = default);
}
