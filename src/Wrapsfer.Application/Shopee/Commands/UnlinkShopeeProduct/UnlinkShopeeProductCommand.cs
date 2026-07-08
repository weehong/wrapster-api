using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Shopee.Commands.UnlinkShopeeProduct;

public sealed record UnlinkShopeeProductCommand(
    string TenantId,
    Guid LinkId) : ICommand;
