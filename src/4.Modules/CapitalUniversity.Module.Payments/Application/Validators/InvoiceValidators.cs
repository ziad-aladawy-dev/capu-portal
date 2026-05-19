using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using FluentValidation;

namespace CapitalUniversity.Modules.Payments.Application.Validators;

public class CreateInvoiceValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one invoice item is required.");
        RuleForEach(x => x.Items).SetValidator(new CreateInvoiceItemValidator());
    }
}

public class CreateInvoiceItemValidator : AbstractValidator<CreateInvoiceItemRequest>
{
    public CreateInvoiceItemValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.FeeType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.SourceModule).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class RecordPaymentValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ProviderTransactionId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m);
    }
}
