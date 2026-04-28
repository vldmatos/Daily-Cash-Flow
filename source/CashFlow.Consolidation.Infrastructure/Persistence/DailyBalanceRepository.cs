using CashFlow.Consolidation.Domain.ReadModels;
using CashFlow.Consolidation.Domain.Repositories;
using Dapper;
using Npgsql;

namespace CashFlow.Consolidation.Infrastructure.Persistence;

public sealed class DailyBalanceRepository(NpgsqlDataSource dataSource) : IDailyBalanceRepository
{
    public async Task<DailyBalance?> GetAsync(Guid merchantId, DateOnly date, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT merchant_id, date, total_credits, total_debits, balance, transaction_count, last_updated_at
            FROM daily_balance
            WHERE merchant_id = @merchantId AND date = @date
            """;
        var row = await conn.QueryFirstOrDefaultAsync<DailyBalanceRow>(
            sql, new { merchantId, date = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        return row is null ? null : MapRow(row);
    }

    public async Task<IReadOnlyList<DailyBalance>> GetRangeAsync(
        Guid merchantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT merchant_id, date, total_credits, total_debits, balance, transaction_count, last_updated_at
            FROM daily_balance
            WHERE merchant_id = @merchantId AND date BETWEEN @from AND @to
            ORDER BY date
            """;
        var rows = await conn.QueryAsync<DailyBalanceRow>(
            sql, new
            {
                merchantId,
                from = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                to = to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            });
        return rows.Select(MapRow).ToList().AsReadOnly();
    }

    public async Task UpsertAsync(
        Guid merchantId, DateOnly date, decimal creditsDelta, decimal debitsDelta,
        int countDelta, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO daily_balance (merchant_id, date, total_credits, total_debits, balance, transaction_count, last_updated_at)
            VALUES (@merchantId, @date, @credits, @debits, @balance, @count, NOW())
            ON CONFLICT (merchant_id, date) DO UPDATE SET
                total_credits   = daily_balance.total_credits + EXCLUDED.total_credits,
                total_debits    = daily_balance.total_debits  + EXCLUDED.total_debits,
                balance         = daily_balance.balance       + (EXCLUDED.total_credits - EXCLUDED.total_debits),
                transaction_count = daily_balance.transaction_count + EXCLUDED.transaction_count,
                last_updated_at = NOW()
            """;
        await conn.ExecuteAsync(sql, new
        {
            merchantId,
            date = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            credits = creditsDelta,
            debits = debitsDelta,
            balance = creditsDelta - debitsDelta,
            count = countDelta
        });
    }

    public async Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = "SELECT EXISTS(SELECT 1 FROM processed_events WHERE event_id = @eventId)";
        return await conn.ExecuteScalarAsync<bool>(sql, new { eventId });
    }

    public async Task MarkEventProcessedAsync(
        Guid eventId, string eventType, DateTimeOffset occurredAt, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO processed_events (event_id, event_type, occurred_at, processed_at)
            VALUES (@eventId, @eventType, @occurredAt, NOW())
            ON CONFLICT (event_id) DO NOTHING
            """;
        await conn.ExecuteAsync(sql, new { eventId, eventType, occurredAt });
    }

    private static DailyBalance MapRow(DailyBalanceRow r) => new()
    {
        MerchantId = r.merchant_id,
        Date = DateOnly.FromDateTime(r.date),
        TotalCredits = r.total_credits,
        TotalDebits = r.total_debits,
        Balance = r.balance,
        TransactionCount = r.transaction_count,
        LastUpdatedAt = r.last_updated_at
    };

    private sealed record DailyBalanceRow(
        Guid merchant_id,
        DateTime date,
        decimal total_credits,
        decimal total_debits,
        decimal balance,
        int transaction_count,
        DateTimeOffset last_updated_at);
}
