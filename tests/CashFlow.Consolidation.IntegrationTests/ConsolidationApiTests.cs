using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.Consolidation.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Npgsql;
using CashFlow.Consolidation.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using WebMotions.Fake.Authentication.JwtBearer;
using Xunit;
using FluentAssertions;

namespace CashFlow.Consolidation.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ConsolidationApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("cashflow_consolidation")
        .WithUsername("cashflow")
        .WithPassword("cashflow_secret")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private readonly Guid _merchantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:ConsolidationDb", _postgres.GetConnectionString());
                builder.UseSetting("Redis:ConnectionString", _redis.GetConnectionString());
                builder.UseSetting("Auth:Authority", "");
                builder.UseSetting("Otel:Endpoint", "http://localhost:4317");
                
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(FakeJwtBearerDefaults.AuthenticationScheme).AddFakeJwtBearer();
                });
            });

        _client = _factory.CreateClient();
        _client.SetFakeBearerToken(new Dictionary<string, object>
        {
            { "sub", Guid.NewGuid().ToString() },
            { "email", "test@example.com" },
            { "merchantId", _merchantId.ToString() }
        });

        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE daily_balance (
                merchant_id UUID NOT NULL,
                date TIMESTAMP NOT NULL,
                total_credits NUMERIC NOT NULL,
                total_debits NUMERIC NOT NULL,
                balance NUMERIC NOT NULL,
                transaction_count INT NOT NULL,
                last_updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
                PRIMARY KEY (merchant_id, date)
            );
            CREATE TABLE processed_events (
                event_id UUID PRIMARY KEY,
                event_type VARCHAR(100) NOT NULL,
                occurred_at TIMESTAMP WITH TIME ZONE NOT NULL,
                processed_at TIMESTAMP WITH TIME ZONE NOT NULL
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task GetDailyBalance_NoTransactions_ReturnsZeroBalance()
    {
        var response = await _client!.GetAsync($"/balance/{_merchantId}");
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        body.Should().NotBeNull();
        body!.Balance.Should().Be(0);
        body.TotalCredits.Should().Be(0);
        body.TotalDebits.Should().Be(0);
        body.TransactionCount.Should().Be(0);
    }
}
