using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;

public sealed class CreateShopeeLinkedProductCommandValidator
    : AbstractValidator<CreateShopeeLinkedProductCommand>
{
    public CreateShopeeLinkedProductCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ShopeeItemId).GreaterThan(0);
        RuleFor(x => x.ShopeeModelId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Barcode).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
    }
}
