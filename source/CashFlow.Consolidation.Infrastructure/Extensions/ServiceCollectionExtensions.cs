using CashFlow.Consolidation.Domain.Repositories;
using CashFlow.Consolidation.Infrastructure.Cache;
using CashFlow.Consolidation.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;

namespace CashFlow.Consolidation.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidationInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string redisConnectionString)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton<DailyBalanceCache>();

        return services;
    }
}
