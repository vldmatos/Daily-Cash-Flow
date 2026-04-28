using CashFlow.Consolidation.Infrastructure.Extensions;
using CashFlow.Consolidation.Service.Consumers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = new HostApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ConsolidationDb")
    ?? throw new InvalidOperationException("ConsolidationDb is required.");
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? "localhost:6379";
var rabbitMqHost = builder.Configuration["RabbitMq:Host"]
    ?? "rabbitmq://guest:guest@localhost/";

builder.Services.AddConsolidationInfrastructure(connectionString, redisConnectionString);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TransactionCreatedConsumer>();
    x.AddConsumer<TransactionReversedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbitMqHost);

        cfg.ReceiveEndpoint("consolidation.transaction.created", e =>
        {
            e.Durable = true;
            e.UseMessageRetry(r => r.Exponential(3,
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(32), TimeSpan.FromSeconds(2)));
            e.DeadLetterExchange = "consolidation.dlq";
            e.ConfigureConsumer<TransactionCreatedConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("consolidation.transaction.reversed", e =>
        {
            e.Durable = true;
            e.UseMessageRetry(r => r.Exponential(3,
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(32), TimeSpan.FromSeconds(2)));
            e.DeadLetterExchange = "consolidation.dlq";
            e.ConfigureConsumer<TransactionReversedConsumer>(ctx);
        });
    });
});

var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("consolidation-service", serviceVersion: "1.0.0"))
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

builder.Services.AddHealthChecks();

var host = builder.Build();

await host.RunAsync();
