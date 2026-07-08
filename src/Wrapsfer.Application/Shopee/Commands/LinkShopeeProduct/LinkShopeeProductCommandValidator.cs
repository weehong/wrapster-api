using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.LinkShopeeProduct;

public sealed class LinkShopeeProductCommandValidator : AbstractValidator<LinkShopeeProductCommand>
{
    public LinkShopeeProductCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ShopeeItemId).GreaterThan(0);
        RuleFor(x => x.ShopeeModelId).GreaterThanOrEqualTo(0);
    }
}
