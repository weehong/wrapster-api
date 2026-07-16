using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

public sealed record ShipShopeeOrderCommand(
    string TenantId,
    Guid OrderId,
    string Method,
    long? AddressId,
    string? PickupTimeId,
    long? BranchId) : ICommand<ShopeeOrderResponse>;
