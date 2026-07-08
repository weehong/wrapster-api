using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.SyncShopeeProductStock;

public sealed class SyncShopeeProductStockCommandValidator
    : AbstractValidator<SyncShopeeProductStockCommand>
{
    public SyncShopeeProductStockCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
