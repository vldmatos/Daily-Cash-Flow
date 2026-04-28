using CashFlow.Transactions.Domain.Repositories;
using CashFlow.Transactions.Infrastructure.Outbox;
using CashFlow.Transactions.Infrastructure.Persistence;
using CashFlow.Transactions.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transactions.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionsInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string rabbitMqHost)
    {
        services.AddDbContext<TransactionDbContext>(opts =>
            opts.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "public");
                npgsql.MigrationsAssembly("CashFlow.Transactions.Api");
            }));

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddHostedService<OutboxPublisher>();

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitMqHost);
                cfg.Durable = true;
                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
