using CashFlow.Consolidation.Domain.ReadModels;
using StackExchange.Redis;

namespace CashFlow.Consolidation.Infrastructure.Cache;

public sealed class DailyBalanceCache(IConnectionMultiplexer redis)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(72);
    private IDatabase Db => redis.GetDatabase();

    private static string Key(Guid merchantId, DateOnly date) =>
        $"balance:{merchantId}:{date:yyyy-MM-dd}";

    public async Task<DailyBalance?> GetAsync(Guid merchantId, DateOnly date)
    {
        var key = Key(merchantId, date);
        var hash = await Db.HashGetAllAsync(key);
        if (hash.Length == 0) return null;
        return MapHash(merchantId, date, hash);
    }

    public async Task SetAsync(DailyBalance balance)
    {
        var key = Key(balance.MerchantId, balance.Date);
        var entries = new[]
        {
            new HashEntry("total_credits", balance.TotalCredits.ToString()),
            new HashEntry("total_debits", balance.TotalDebits.ToString()),
            new HashEntry("balance", balance.Balance.ToString()),
            new HashEntry("transaction_count", balance.TransactionCount.ToString()),
            new HashEntry("last_updated_at", balance.LastUpdatedAt.ToString("O"))
        };
        var batch = Db.CreateBatch();
        _ = batch.HashSetAsync(key, entries);
        _ = batch.KeyExpireAsync(key, Ttl);
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task IncrementAsync(
        Guid merchantId, DateOnly date,
        decimal creditsDelta, decimal debitsDelta, int countDelta)
    {
        var key = Key(merchantId, date);
        var exists = await Db.KeyExistsAsync(key);
        if (!exists) return;

        var balanceDelta = creditsDelta - debitsDelta;
        var batch = Db.CreateBatch();
        if (creditsDelta != 0)
            _ = batch.HashIncrementAsync(key, "total_credits", (double)creditsDelta);
        if (debitsDelta != 0)
            _ = batch.HashIncrementAsync(key, "total_debits", (double)debitsDelta);
        if (balanceDelta != 0)
            _ = batch.HashIncrementAsync(key, "balance", (double)balanceDelta);
        if (countDelta != 0)
            _ = batch.HashIncrementAsync(key, "transaction_count", countDelta);
        _ = batch.HashSetAsync(key, [new HashEntry("last_updated_at", DateTimeOffset.UtcNow.ToString("O"))]);
        _ = batch.KeyExpireAsync(key, Ttl);
        batch.Execute();
        await Task.CompletedTask;
    }

    private static DailyBalance MapHash(Guid merchantId, DateOnly date, HashEntry[] hash)
    {
        var d = hash.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
        return new DailyBalance
        {
            MerchantId = merchantId,
            Date = date,
            TotalCredits = decimal.Parse(d.GetValueOrDefault("total_credits", "0")!),
            TotalDebits = decimal.Parse(d.GetValueOrDefault("total_debits", "0")!),
            Balance = decimal.Parse(d.GetValueOrDefault("balance", "0")!),
            TransactionCount = int.Parse(d.GetValueOrDefault("transaction_count", "0")!),
            LastUpdatedAt = DateTimeOffset.Parse(d.GetValueOrDefault("last_updated_at", DateTimeOffset.UtcNow.ToString("O"))!)
        };
    }
}
