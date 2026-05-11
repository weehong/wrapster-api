using FluentValidation;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Barcode).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
        RuleFor(x => x.Type).IsInEnum();

        RuleFor(x => x.StockQuantity)
            .Equal(0)
            .WithMessage("Stock quantity must be 0 for bundle products; bundle stock is derived from components.")
            .When(x => x.Type == ProductType.Bundle);

        RuleFor(x => x.UnpackTargetProductId)
            .NotEmpty()
            .When(x => x.Type == ProductType.Package);

        RuleFor(x => x.UnpackQuantityPerPackage)
            .NotNull()
            .GreaterThan(0)
            .When(x => x.Type == ProductType.Package);

        RuleFor(x => x.Components)
            .NotNull()
            .Must(c => c!.Count > 0)
            .WithMessage("A bundle must have at least one component.")
            .When(x => x.Type == ProductType.Bundle);

        RuleFor(x => x.Components)
            .Must(c => c!.Select(ci => ci.ChildProductId).Distinct().Count() == c!.Count)
            .WithMessage("A bundle cannot contain duplicate component products.")
            .When(x => x.Type == ProductType.Bundle && x.Components is { Count: > 0 });

        RuleForEach(x => x.Components)
            .ChildRules(c =>
            {
                c.RuleFor(x => x.ChildProductId).NotEmpty();
                c.RuleFor(x => x.Quantity).GreaterThan(0);
            })
            .When(x => x.Type == ProductType.Bundle && x.Components != null);
    }
}
