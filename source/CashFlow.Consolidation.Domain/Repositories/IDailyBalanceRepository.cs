using CashFlow.Consolidation.Domain.ReadModels;

namespace CashFlow.Consolidation.Domain.Repositories;

public interface IDailyBalanceRepository
{
    Task<DailyBalance?> GetAsync(Guid merchantId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<DailyBalance>> GetRangeAsync(Guid merchantId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task UpsertAsync(Guid merchantId, DateOnly date, decimal creditsDelta, decimal debitsDelta, int countDelta, CancellationToken ct = default);
    Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken ct = default);
    Task MarkEventProcessedAsync(Guid eventId, string eventType, DateTimeOffset occurredAt, CancellationToken ct = default);
}
