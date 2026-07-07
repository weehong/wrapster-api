using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Wrapsfer.Application.Billing;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;
using BillingPortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;
using BillingPortalSessionService = Stripe.BillingPortal.SessionService;

namespace Wrapsfer.Infrastructure.Billing;

internal sealed class StripeBillingGateway : IStripeBillingGateway
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripeBillingGateway> _logger;
    private readonly CustomerService _customerService;
    private readonly SessionService _checkoutSessionService;
    private readonly BillingPortalSessionService _portalSessionService;
    private readonly RefundService _refundService;
    private readonly InvoiceService _invoiceService;

    public StripeBillingGateway(IOptions<StripeOptions> options, ILogger<StripeBillingGateway> logger)
    {
        _options = options.Value;
        _logger = logger;

        StripeClient client = new(_options.SecretKey);
        _customerService = new CustomerService(client);
        _checkoutSessionService = new SessionService(client);
        _portalSessionService = new BillingPortalSessionService(client);
        _refundService = new RefundService(client);
        _invoiceService = new InvoiceService(client);
    }

    public async Task<string> CreateCustomerAsync(
        string tenantId,
        string? email,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        CustomerCreateOptions createOptions = new()
        {
            Email = email,
            Name = string.IsNullOrWhiteSpace(displayName) ? tenantId : displayName,
            Metadata = new Dictionary<string, string> { [BillingMetadataKeys.TenantId] = tenantId }
        };

        // Stable key per tenant: concurrent first-use requests and SDK retries resolve to the same
        // Stripe customer instead of creating duplicates.
        RequestOptions requestOptions = new() { IdempotencyKey = $"customer-create:{tenantId}" };

        Customer customer = await _customerService.CreateAsync(createOptions, requestOptions, cancellationToken);
        return customer.Id;
    }

    public async Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        StripeCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        SessionCreateOptions createOptions = new()
        {
            Mode = "payment",
            Customer = request.CustomerId,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            InvoiceCreation = new SessionInvoiceCreationOptions { Enabled = true },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = request.AmountMinor,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.ProductName
                        }
                    }
                }
            ],
            Metadata = new Dictionary<string, string>(request.Metadata)
        };

        RequestOptions requestOptions = new() { IdempotencyKey = request.IdempotencyKey };

        Session session = await _checkoutSessionService.CreateAsync(createOptions, requestOptions, cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            // Without a redirect URL the session is unusable; surface it as a failure rather than
            // returning an empty URL the caller would hand to the browser.
            throw new InvalidOperationException(
                $"Stripe returned checkout session {session.Id} without a redirect URL.");
        }

        return new StripeCheckoutSessionResult(session.Id, session.Url);
    }

    public async Task<string> CreatePortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        BillingPortalSessionCreateOptions createOptions = new()
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        };

        Stripe.BillingPortal.Session session =
            await _portalSessionService.CreateAsync(createOptions, null, cancellationToken);
        return session.Url;
    }

    public async Task<IReadOnlyList<StripeBillingHistoryItem>> ListBillingHistoryAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        // Limit is the page size; ListAutoPagingAsync transparently fetches every page so a customer
        // with a long history is not silently truncated to the first page.
        InvoiceListOptions invoiceOptions = new()
        {
            Customer = customerId,
            Limit = 50
        };
        invoiceOptions.AddExpand("data.payments");

        List<StripeBillingHistoryItem> items = [];

        await foreach (Invoice invoice in _invoiceService.ListAutoPagingAsync(
            invoiceOptions, null, cancellationToken))
        {
            string? paymentIntentId = invoice.Payments?.Data
                .Select(p => p.Payment?.PaymentIntentId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

            items.Add(new StripeBillingHistoryItem(
                invoice.Id,
                "Invoice",
                invoice.Status ?? "unknown",
                invoice.Created,
                checked((int)(invoice.AmountPaid > 0 ? invoice.AmountPaid : invoice.AmountDue)),
                invoice.Currency,
                invoice.Description ?? invoice.Number ?? "Invoice",
                invoice.HostedInvoiceUrl,
                invoice.InvoicePdf,
                invoice.Id,
                paymentIntentId,
                null));

            if (string.IsNullOrWhiteSpace(paymentIntentId))
            {
                continue;
            }

            RefundListOptions refundOptions = new()
            {
                PaymentIntent = paymentIntentId,
                Limit = 20
            };

            await foreach (Refund refund in _refundService.ListAutoPagingAsync(
                refundOptions, null, cancellationToken))
            {
                items.Add(new StripeBillingHistoryItem(
                    refund.Id,
                    "Refund",
                    refund.Status ?? "unknown",
                    refund.Created,
                    checked((int)refund.Amount),
                    refund.Currency,
                    refund.Description ?? $"Refund for {invoice.Number ?? invoice.Id}",
                    invoice.HostedInvoiceUrl,
                    invoice.InvoicePdf,
                    invoice.Id,
                    paymentIntentId,
                    refund.Id));
            }
        }

        return items
            .OrderByDescending(i => i.OccurredAtUtc)
            .ToList();
    }

    public async Task<string> RefundPaymentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        RefundCreateOptions createOptions = new()
        {
            PaymentIntent = paymentIntentId
        };

        // Stable key per payment intent: a retried refund (e.g. after a failed local commit) resolves
        // to the same Stripe refund instead of refunding the customer twice.
        RequestOptions requestOptions = new() { IdempotencyKey = $"refund:{paymentIntentId}" };

        Refund refund = await _refundService.CreateAsync(createOptions, requestOptions, cancellationToken);
        return refund.Id;
    }

    public Result<StripeWebhookNotification> ParseWebhookEvent(string payload, string signatureHeader)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _options.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return Result<StripeWebhookNotification>.Failure(BillingErrors.WebhookSignatureInvalid);
        }

        string? checkoutSessionId = null;
        string? paymentIntentId = null;
        string? invoiceId = null;
        string? customerId = null;
        IReadOnlyDictionary<string, string> metadata = new Dictionary<string, string>();

        if (stripeEvent.Data.Object is Session session)
        {
            checkoutSessionId = session.Id;
            paymentIntentId = session.PaymentIntentId;
            invoiceId = session.InvoiceId;
            customerId = session.CustomerId;
            if (session.Metadata is not null)
            {
                metadata = session.Metadata;
            }
        }
        else if (stripeEvent.Data.Object is Charge charge)
        {
            paymentIntentId = charge.PaymentIntentId;
            customerId = charge.CustomerId;
            if (charge.Metadata is not null)
            {
                metadata = charge.Metadata;
            }
        }
        else if (stripeEvent.Data.Object is Refund refund)
        {
            paymentIntentId = refund.PaymentIntentId;
            if (refund.Metadata is not null)
            {
                metadata = refund.Metadata;
            }
        }

        StripeWebhookNotification notification = new(
            stripeEvent.Id,
            stripeEvent.Type,
            checkoutSessionId,
            paymentIntentId,
            invoiceId,
            customerId,
            metadata);

        return Result<StripeWebhookNotification>.Success(notification);
    }
}
