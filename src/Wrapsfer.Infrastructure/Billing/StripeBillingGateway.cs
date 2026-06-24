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

        Customer customer = await _customerService.CreateAsync(createOptions, null, cancellationToken);
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

        Session session = await _checkoutSessionService.CreateAsync(createOptions, null, cancellationToken);
        return new StripeCheckoutSessionResult(session.Id, session.Url ?? string.Empty);
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
        InvoiceListOptions invoiceOptions = new()
        {
            Customer = customerId,
            Limit = 50
        };
        invoiceOptions.AddExpand("data.payments");

        StripeList<Invoice> invoices = await _invoiceService.ListAsync(
            invoiceOptions, null, cancellationToken);

        List<StripeBillingHistoryItem> items = [];

        foreach (Invoice invoice in invoices.Data)
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

            StripeList<Refund> refunds = await _refundService.ListAsync(
                refundOptions, null, cancellationToken);

            foreach (Refund refund in refunds.Data)
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

        Refund refund = await _refundService.CreateAsync(createOptions, null, cancellationToken);
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
