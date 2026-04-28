namespace CashFlow.Consolidation.Domain.ReadModels;

public sealed record DailyBalance
{
    public required Guid MerchantId { get; init; }
    public required DateOnly Date { get; init; }
    public required decimal TotalCredits { get; init; }
    public required decimal TotalDebits { get; init; }
    public required decimal Balance { get; init; }
    public required int TransactionCount { get; init; }
    public required DateTimeOffset LastUpdatedAt { get; init; }
}
