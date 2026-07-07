using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Shopee.Commands.DisconnectShopeeShop;

public sealed record DisconnectShopeeShopCommand(string TenantId) : ICommand;
