using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Application.Billing.Commands.HandleStripeWebhook;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Api.Controllers.V1;

[AllowAnonymous]
[Route("api/v{version:apiVersion}/stripe")]
public sealed class StripeController(ISender sender) : ApiControllerBase
{
    private const string SignatureHeaderName = "Stripe-Signature";

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        // The signature is computed over the exact raw bytes, so the body must be read
        // verbatim — never via model binding.
        using StreamReader reader = new(Request.Body);
        string payload = await reader.ReadToEndAsync(cancellationToken);

        string signature = Request.Headers[SignatureHeaderName].ToString();

        Result result = await sender.Send(
            new HandleStripeWebhookCommand(payload, signature), cancellationToken);

        return ToActionResult(result);
    }
}
