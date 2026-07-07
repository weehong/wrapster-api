using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.CompleteShopeeAuthorization;

public sealed class CompleteShopeeAuthorizationCommandValidator
    : AbstractValidator<CompleteShopeeAuthorizationCommand>
{
    public CompleteShopeeAuthorizationCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.ShopId).GreaterThan(0);
    }
}
