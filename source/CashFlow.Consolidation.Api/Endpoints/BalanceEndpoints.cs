using CashFlow.Consolidation.Domain.ReadModels;
using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Cache;

namespace CashFlow.Consolidation.Api.Endpoints;

public static class BalanceEndpoints
{
    public static void MapBalanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/balance")
            .RequireAuthorization()
            .WithTags("Balance");

        group.MapGet("/{merchantId:guid}", GetDailyBalance)
            .WithName("GetDailyBalance")
            .Produces<BalanceResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{merchantId:guid}/range", GetBalanceRange)
            .WithName("GetBalanceRange")
            .Produces<IEnumerable<BalanceResponse>>();
    }

    private static async Task<IResult> GetDailyBalance(
        Guid merchantId,
        DateOnly? date,
        IDailyBalanceRepository repository,
        DailyBalanceCache cache,
        CancellationToken ct)
    {
        var queryDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var cached = await cache.GetAsync(merchantId, queryDate);
        if (cached is not null)
            return Results.Ok(MapResponse(cached));

        var balance = await repository.GetAsync(merchantId, queryDate, ct);

        if (balance is not null)
        {
            await cache.SetAsync(balance);
            return Results.Ok(MapResponse(balance));
        }

        return Results.Ok(new BalanceResponse(merchantId, queryDate, 0, 0, 0, 0, DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> GetBalanceRange(
        Guid merchantId,
        DateOnly from,
        DateOnly to,
        IDailyBalanceRepository repository,
        CancellationToken ct)
    {
        if ((to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).Days > 90)
            return Results.BadRequest(new { error = "Date range cannot exceed 90 days." });

        var balances = await repository.GetRangeAsync(merchantId, from, to, ct);
        return Results.Ok(balances.Select(MapResponse));
    }

    private static BalanceResponse MapResponse(DailyBalance b) =>
        new(b.MerchantId, b.Date, b.TotalCredits, b.TotalDebits, b.Balance,
            b.TransactionCount, b.LastUpdatedAt);
}

public sealed record BalanceResponse(
    Guid MerchantId,
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance,
    int TransactionCount,
    DateTimeOffset ComputedAt);
