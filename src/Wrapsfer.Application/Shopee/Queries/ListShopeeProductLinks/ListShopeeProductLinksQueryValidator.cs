using FluentValidation;

namespace Wrapsfer.Application.Shopee.Queries.ListShopeeProductLinks;

public sealed class ListShopeeProductLinksQueryValidator
    : AbstractValidator<ListShopeeProductLinksQuery>
{
    public ListShopeeProductLinksQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
