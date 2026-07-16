using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Shopee.Commands.IngestShopeeWebhook;

public sealed record IngestShopeeWebhookCommand(string Body, string? AuthorizationHeader) : ICommand;
