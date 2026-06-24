using FluentValidation;

namespace Wrapsfer.Application.Billing.Commands.HandleStripeWebhook;

public sealed class HandleStripeWebhookCommandValidator : AbstractValidator<HandleStripeWebhookCommand>
{
    public HandleStripeWebhookCommandValidator()
    {
        RuleFor(x => x.Payload).NotEmpty();
        RuleFor(x => x.SignatureHeader).NotEmpty();
    }
}
