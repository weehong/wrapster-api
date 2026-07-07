using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Commands.CompleteShopeeAuthorization;

public sealed record CompleteShopeeAuthorizationCommand(
    string TenantId,
    string Code,
    long ShopId) : ICommand<ShopeeConnectionResponse>;
