using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.DisconnectShopeeShop;

public sealed class DisconnectShopeeShopCommandValidator : AbstractValidator<DisconnectShopeeShopCommand>
{
    public DisconnectShopeeShopCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
