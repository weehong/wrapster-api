using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

internal sealed class ShipShopeeOrderCommandValidator : AbstractValidator<ShipShopeeOrderCommand>
{
    public ShipShopeeOrderCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Method)
            .Must(m => m is "pickup" or "dropoff")
            .WithMessage("Method must be pickup or dropoff");
        When(c => c.Method == "pickup", () =>
        {
            RuleFor(c => c.AddressId).NotNull();
            RuleFor(c => c.PickupTimeId).NotEmpty();
        });
    }
}
