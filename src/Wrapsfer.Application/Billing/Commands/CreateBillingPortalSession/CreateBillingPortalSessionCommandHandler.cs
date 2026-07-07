using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Billing.Commands.CreateBillingPortalSession;

internal sealed class CreateBillingPortalSessionCommandHandler(
    ITenantContext tenantContext,
    StripeCustomerProvisioner customerProvisioner,
    IStripeBillingGateway billingGateway,
    IUnitOfWork unitOfWork,
    IOptions<StripeOptions> stripeOptions)
    : ICommandHandler<CreateBillingPortalSessionCommand, CreateBillingPortalSessionResult>
{
    public async Task<Result<CreateBillingPortalSessionResult>> Handle(
        CreateBillingPortalSessionCommand request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;
        StripeOptions options = stripeOptions.Value;

        Result<string> customerResult = await customerProvisioner.EnsureCustomerIdAsync(tenantId, cancellationToken);
        if (customerResult.IsFailure)
        {
            return Result<CreateBillingPortalSessionResult>.Failure(customerResult.Error);
        }

        // Persist the customer mapping if it was created on demand for this portal visit.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        string returnUrl = BillingUrlBuilder.Combine(options.FrontendBaseUrl, options.BillingPortalReturnPath);
        string portalUrl = await billingGateway.CreatePortalSessionAsync(
            customerResult.Value, returnUrl, cancellationToken);

        return Result<CreateBillingPortalSessionResult>.Success(
            new CreateBillingPortalSessionResult(portalUrl));
    }
}
