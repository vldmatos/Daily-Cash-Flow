namespace CashFlow.Transactions.Infrastructure.Persistence;

public sealed class IdempotencyKey
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public required string Key { get; set; }
    public required string ResponseJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
