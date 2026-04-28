using CashFlow.Transactions.Api.Application.Commands.CreateTransaction;
using CashFlow.Transactions.Api.Application.Commands.ReverseTransaction;
using CashFlow.Transactions.Api.Application.Queries.GetTransaction;
using CashFlow.Transactions.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Transactions.Api.Endpoints;

public static class TransactionsEndpoints
{
    public static void MapTransactionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/transactions")
            .RequireAuthorization()
            .WithTags("Transactions");

        group.MapPost("/", CreateTransaction)
            .WithName("CreateTransaction")
            .Produces<CreateTransactionResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", GetTransaction)
            .WithName("GetTransaction")
            .Produces<GetTransactionResult>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reverse", ReverseTransaction)
            .WithName("ReverseTransaction")
            .Produces<ReverseTransactionResult>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateTransaction(
        [FromBody] CreateTransactionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var merchantId = GetMerchantId(httpContext);

        var command = new CreateTransactionCommand
        {
            MerchantId = merchantId,
            Type = Enum.Parse<TransactionType>(request.Type, ignoreCase: true),
            Amount = request.Amount,
            Currency = request.Currency ?? "BRL",
            OccurredOn = request.OccurredOn,
            Description = request.Description,
            IdempotencyKey = idempotencyKey
        };

        try
        {
            var result = await mediator.Send(command, ct);
            return Results.Created($"/transactions/{result.Id}", result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetTransaction(
        Guid id,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var merchantId = GetMerchantId(httpContext);
        var result = await mediator.Send(new GetTransactionQuery(id, merchantId), ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ReverseTransaction(
        Guid id,
        [FromBody] ReverseTransactionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var merchantId = GetMerchantId(httpContext);

        var command = new ReverseTransactionCommand
        {
            TransactionId = id,
            MerchantId = merchantId,
            Reason = request.Reason,
            IdempotencyKey = idempotencyKey
        };

        var result = await mediator.Send(command, ct);
        return Results.Created($"/transactions/{result.ReversalId}", result);
    }

    private static Guid GetMerchantId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst("merchantId")?.Value
            ?? httpContext.User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("merchantId claim not found.");
        return Guid.Parse(claim);
    }
}

public sealed record CreateTransactionRequest(
    string Type,
    decimal Amount,
    DateTimeOffset OccurredOn,
    string? Currency,
    string? Description);

public sealed record ReverseTransactionRequest(string? Reason);
