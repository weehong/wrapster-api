using FluentValidation;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeShopItems;

public sealed class GetShopeeShopItemsQueryValidator : AbstractValidator<GetShopeeShopItemsQuery>
{
    public GetShopeeShopItemsQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 20);
    }
}
