using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.UnlinkShopeeProduct;

public sealed class UnlinkShopeeProductCommandValidator : AbstractValidator<UnlinkShopeeProductCommand>
{
    public UnlinkShopeeProductCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.LinkId).NotEmpty();
    }
}
