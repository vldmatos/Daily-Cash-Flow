using CashFlow.Transactions.Domain.Aggregates;
using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.Infrastructure.Repositories;

internal sealed class TransactionRepository(TransactionDbContext db) : ITransactionRepository
{
    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.Transactions.FindAsync([id], cancellationToken);

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await db.Transactions.AddAsync(transaction, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        db.Transactions.Update(transaction);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsIdempotencyKeyAsync(
        Guid merchantId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await db.IdempotencyKeys
            .AnyAsync(k => k.MerchantId == merchantId
                        && k.Key == idempotencyKey
                        && k.ExpiresAt > DateTimeOffset.UtcNow,
                cancellationToken);

    public async Task SaveIdempotencyKeyAsync(
        Guid merchantId, string idempotencyKey, string responseJson,
        CancellationToken cancellationToken = default)
    {
        var key = new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Key = idempotencyKey,
            ResponseJson = responseJson,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };
        await db.IdempotencyKeys.AddAsync(key, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetIdempotencyResponseAsync(
        Guid merchantId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await db.IdempotencyKeys
            .Where(k => k.MerchantId == merchantId
                     && k.Key == idempotencyKey
                     && k.ExpiresAt > DateTimeOffset.UtcNow)
            .Select(k => k.ResponseJson)
            .FirstOrDefaultAsync(cancellationToken);
}
