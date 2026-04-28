using CashFlow.Transactions.Domain.Enums;
using FluentValidation;

namespace CashFlow.Transactions.Api.Application.Commands.CreateTransaction;

internal sealed class CreateTransactionValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionValidator()
    {
        RuleFor(x => x.MerchantId).NotEmpty().WithMessage("MerchantId is required.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Type must be Credit or Debit.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
        RuleFor(x => x.Currency).Length(3).WithMessage("Currency must be a 3-letter ISO code.")
            .When(x => x.Currency is not null);
        RuleFor(x => x.OccurredOn)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("OccurredOn cannot be more than 5 minutes in the future.");
        RuleFor(x => x.Description).MaximumLength(140)
            .When(x => x.Description is not null);
    }
}
