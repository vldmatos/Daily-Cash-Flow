using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.Transactions.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using CashFlow.Transactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using WebMotions.Fake.Authentication.JwtBearer;

namespace CashFlow.Transactions.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TransactionsApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("cashflow_tx")
        .WithUsername("cashflow")
        .WithPassword("cashflow_secret")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:TransactionsDb", _postgres.GetConnectionString());
                builder.UseSetting("RabbitMq:Host", $"rabbitmq://guest:guest@{_rabbit.Hostname}:{_rabbit.GetMappedPublicPort(5672)}/");
                builder.UseSetting("Auth:Authority", "");
                builder.UseSetting("Otel:Endpoint", "http://localhost:4317");
            })
            .WithWebHostBuilder(builder => 
            {
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
            { "merchantId", Guid.NewGuid().ToString() }
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    [Fact]
    public async Task PostTransaction_WithValidData_Returns201()
    {
        var merchantId = Guid.NewGuid();
        var request = new CreateTransactionRequest(
            "Credit", 100.50m, DateTimeOffset.UtcNow.AddMinutes(-5), "BRL", "Venda teste");

        var response = await _client!.PostAsJsonAsync("/transactions", request);

        var bodyText = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, because: bodyText);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        body.GetProperty("status").GetString().Should().Be("Accepted");
    }

    [Fact]
    public async Task PostTransaction_WithNegativeAmount_Returns400()
    {
        var request = new CreateTransactionRequest(
            "Credit", -50m, DateTimeOffset.UtcNow, "BRL", null);

        var response = await _client!.PostAsJsonAsync("/transactions", request);

        var bodyText = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: bodyText);
    }

    [Fact]
    public async Task GetTransaction_ExistingId_Returns200()
    {
        var createReq = new CreateTransactionRequest(
            "Debit", 75.00m, DateTimeOffset.UtcNow.AddMinutes(-1), "BRL", "Compra");

        var createResp = await _client!.PostAsJsonAsync("/transactions", createReq);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var getResp = await _client!.GetAsync($"/transactions/{id}");

        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be("Debit");
    }

    [Fact]
    public async Task GetTransaction_NonExistingId_Returns404()
    {
        var resp = await _client!.GetAsync($"/transactions/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
