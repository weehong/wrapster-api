using FluentValidation;

namespace Wrapsfer.Application.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);

        RuleFor(x => x.Components)
            .Must(c => c!.Count > 0)
            .WithMessage("Components must contain at least one entry.")
            .When(x => x.Components is not null);

        RuleFor(x => x.Components)
            .Must(c => c!.Select(ci => ci.ChildProductId).Distinct().Count() == c!.Count)
            .WithMessage("Components cannot contain duplicate child products.")
            .When(x => x.Components is { Count: > 0 });

        RuleForEach(x => x.Components)
            .ChildRules(c =>
            {
                c.RuleFor(x => x.ChildProductId).NotEmpty();
                c.RuleFor(x => x.Quantity).GreaterThan(0);
            })
            .When(x => x.Components is not null);
    }
}
