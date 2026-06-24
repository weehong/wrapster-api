using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Billing.Commands.HandleStripeWebhook;

public sealed record HandleStripeWebhookCommand(string Payload, string SignatureHeader) : ICommand;
