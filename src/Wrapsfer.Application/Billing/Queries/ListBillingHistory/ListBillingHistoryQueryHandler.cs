using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Queries.ListBillingHistory;

internal sealed class ListBillingHistoryQueryHandler(
    ITenantContext tenantContext,
    IPartnerBillingCustomerRepository billingCustomerRepository,
    IFeatureEntitlementRepository entitlementRepository,
    IStripeBillingGateway billingGateway,
    IOptions<StripeOptions> stripeOptions)
    : IQueryHandler<ListBillingHistoryQuery, IReadOnlyList<BillingHistoryItemResponse>>
{
    public async Task<Result<IReadOnlyList<BillingHistoryItemResponse>>> Handle(
        ListBillingHistoryQuery request,
        CancellationToken cancellationToken)
    {
        PartnerBillingCustomer? customer = await billingCustomerRepository.GetByTenantIdAsync(
            tenantContext.TenantId, cancellationToken);

        if (customer is null)
        {
            return Result<IReadOnlyList<BillingHistoryItemResponse>>.Success([]);
        }

        IReadOnlyList<StripeBillingHistoryItem> items = await billingGateway.ListBillingHistoryAsync(
            customer.StripeCustomerId, cancellationToken);
        IReadOnlyList<FeatureEntitlement> entitlements =
            await entitlementRepository.ListByTenantAndFeatureAsync(
                tenantContext.TenantId, BillingFeature.StockReport, cancellationToken);

        TimeZoneInfo billingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(stripeOptions.Value.BillingTimeZone);

        return Result<IReadOnlyList<BillingHistoryItemResponse>>.Success(
            items.Select(i =>
            {
                FeatureEntitlement? entitlement = FindEntitlement(i, entitlements);
                // Both access bounds come from the matched entitlement's window so the displayed
                // period reflects the access granted, not when the transaction happened to occur.
                DateOnly? accessStartDate = entitlement is null
                    ? null
                    : DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(entitlement.ValidFromUtc, billingTimeZone));
                DateOnly? accessEndDate = entitlement is null
                    ? null
                    : DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(entitlement.ValidToUtc, billingTimeZone)
                        .AddDays(-1));

                return new BillingHistoryItemResponse(
                    i.Id,
                    i.Type,
                    i.Status,
                    i.OccurredAtUtc,
                    i.AmountMinor,
                    i.Currency,
                    i.Description,
                    accessStartDate,
                    accessEndDate,
                    i.HostedInvoiceUrl,
                    i.InvoicePdfUrl,
                    i.StripeInvoiceId,
                    i.StripePaymentIntentId,
                    i.StripeRefundId);
            })
                .ToList());
    }

    private static FeatureEntitlement? FindEntitlement(
        StripeBillingHistoryItem item,
        IReadOnlyList<FeatureEntitlement> entitlements) =>
        entitlements.FirstOrDefault(e =>
            (!string.IsNullOrWhiteSpace(item.StripeInvoiceId)
             && e.StripeInvoiceId == item.StripeInvoiceId)
            || (!string.IsNullOrWhiteSpace(item.StripePaymentIntentId)
                && e.StripePaymentIntentId == item.StripePaymentIntentId));
}
