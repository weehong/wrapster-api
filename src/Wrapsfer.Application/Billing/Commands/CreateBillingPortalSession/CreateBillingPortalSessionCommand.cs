using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Billing.Commands.CreateBillingPortalSession;

public sealed record CreateBillingPortalSessionCommand : ICommand<CreateBillingPortalSessionResult>;
