using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Options;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Application.Waybills.Commands.EmailWaybillsReport;

internal sealed class EmailWaybillsReportCommandHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    IPartnerTenantRepository partnerTenantRepository,
    IWaybillReportFileWriter writer,
    IReportStorage reportStorage,
    IMailer mailer,
    ITenantContext tenantContext,
    IOptions<WaybillEmailReportOptions> options,
    ILogger<EmailWaybillsReportCommandHandler> logger)
    : ICommandHandler<EmailWaybillsReportCommand, EmailWaybillsReportResult>
{
    private const int ExportPageSize = 500;
    private readonly WaybillEmailReportOptions _options = options.Value;

    public async Task<Result<EmailWaybillsReportResult>> Handle(
        EmailWaybillsReportCommand request,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<string>> tenantIdsResult =
            await ResolveTenantIdsAsync(request, cancellationToken);
        if (tenantIdsResult.IsFailure)
        {
            return Result<EmailWaybillsReportResult>.Failure(tenantIdsResult.Error);
        }

        IReadOnlyList<string> tenantIds = tenantIdsResult.Value;
        List<Waybill> waybills = await LoadWaybillsAsync(request, tenantIds, cancellationToken);

        HashSet<Guid> productIds = waybills
            .SelectMany(w => w.Items)
            .Select(i => i.ProductId)
            .ToHashSet();

        IReadOnlyDictionary<Guid, Product> productsById = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : (await productRepository.GetByIdsByTenantIdsAsync(productIds, tenantIds, cancellationToken))
            .ToDictionary(p => p.Id);

        List<WaybillReportRow> rows = waybills.SelectMany(w => ToRows(w, productsById)).ToList();
        DateTime requestedAt = DateTime.UtcNow;

        WaybillReportMetadata metadata = new(
            request.From,
            request.To,
            tenantContext.DisplayName ?? tenantContext.Username,
            requestedAt);

        byte[] fileBytes = await writer.WriteAsync(rows, metadata, request.Format, cancellationToken);
        (string contentType, string extension) = GetFileInfo(request.Format);
        string fileName = $"waybills-report-{requestedAt:yyyyMMdd-HHmmss}{extension}";

        bool useAttachment = rows.Count <= _options.AttachmentRowThreshold
                             && fileBytes.LongLength <= _options.AttachmentMaxBytes;

        MailMessage mail = useAttachment
            ? BuildAttachmentMail(request.RecipientEmails, fileName, contentType, fileBytes, rows.Count, request.Format)
            : await BuildLinkMailAsync(
                request.RecipientEmails, fileName, contentType, fileBytes, rows.Count, request.Format,
                requestedAt, cancellationToken);

        MailRequestId requestId = await mailer.SendAsync(mail, cancellationToken);

        WaybillExportDeliveryMode mode =
            useAttachment ? WaybillExportDeliveryMode.Attachment : WaybillExportDeliveryMode.Link;

        logger.LogInformation(
            "Enqueued waybills report mail {MailRequestId} ({Count} rows, format {Format}, mode {Mode}) " +
            "to {RecipientCount} recipient(s)",
            requestId, rows.Count, request.Format, mode, request.RecipientEmails.Count);

        return new EmailWaybillsReportResult(mode, rows.Count);
    }

    private async Task<Result<IReadOnlyList<string>>> ResolveTenantIdsAsync(
        EmailWaybillsReportCommand request,
        CancellationToken cancellationToken)
    {
        if (request.PartnerTenantIds is { Count: > 0 } requestedPartnerIds)
        {
            if (!request.IncludeAllPartnerTenants)
            {
                return Result<IReadOnlyList<string>>.Failure(WaybillExportErrors.PartnerScopeNotAllowed);
            }

            IReadOnlyList<PartnerTenant> activePartners =
                await partnerTenantRepository.ListAsync(isActive: true, cancellationToken);
            HashSet<string> activeIds = activePartners
                .Select(p => p.TenantId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<string> requested = requestedPartnerIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (requested.Count == 0 || requested.Any(id => !activeIds.Contains(id)))
            {
                return Result<IReadOnlyList<string>>.Failure(WaybillExportErrors.UnknownOrInactivePartner);
            }

            return requested;
        }

        if (request.IncludeAllPartnerTenants)
        {
            IReadOnlyList<PartnerTenant> activePartners =
                await partnerTenantRepository.ListAsync(isActive: true, cancellationToken);
            return activePartners.Select(p => p.TenantId).ToList();
        }

        return new[] { tenantContext.TenantId };
    }

    private async Task<List<Waybill>> LoadWaybillsAsync(
        EmailWaybillsReportCommand request,
        IReadOnlyList<string> tenantIds,
        CancellationToken cancellationToken)
    {
        List<Waybill> all = [];
        int page = 1;
        int totalCount;
        do
        {
            (IReadOnlyList<Waybill> items, int total) = await waybillRepository.ListByTenantIdsAsync(
                tenantIds,
                request.From,
                request.To,
                request.Status,
                request.Search,
                page,
                ExportPageSize,
                cancellationToken);
            all.AddRange(items);
            totalCount = total;
            page++;
        } while (all.Count < totalCount);

        return all;
    }

    private static MailMessage BuildAttachmentMail(
        IReadOnlyList<string> recipients,
        string fileName,
        string contentType,
        byte[] fileBytes,
        int rowCount,
        WaybillExportFormat format) => new()
        {
            To = recipients,
            Subject = "Waybill report export",
            Body = MailBody.FromText(BuildAttachmentBody(fileName, format, rowCount)),
            Attachments = [new MailAttachment(fileName, fileBytes, contentType)]
        };

    private async Task<MailMessage> BuildLinkMailAsync(
        IReadOnlyList<string> recipients,
        string fileName,
        string contentType,
        byte[] fileBytes,
        int rowCount,
        WaybillExportFormat format,
        DateTime requestedAt,
        CancellationToken cancellationToken)
    {
        string objectKey =
            $"waybills/{tenantContext.TenantId}/{requestedAt:yyyy-MM-dd}/{fileName}";

        StoredReport stored =
            await reportStorage.UploadAsync(objectKey, fileBytes, contentType, cancellationToken);

        return new MailMessage
        {
            To = recipients,
            Subject = "Waybill report export",
            Body = MailBody.FromText(BuildLinkBody(fileName, format, rowCount, requestedAt, stored))
        };
    }

    private static string BuildAttachmentBody(string reportName, WaybillExportFormat format, int rowCount) =>
        $"""
         Your waybill report export is attached.

         Report: {reportName}
         Format: {format}
         Rows: {rowCount}
         """;

    private static string BuildLinkBody(
        string reportName,
        WaybillExportFormat format,
        int rowCount,
        DateTime requestedAt,
        StoredReport stored) =>
        $"""
         Your waybill report export is ready. The report was too large to attach directly.

         Report: {reportName}
         Format: {format}
         Rows: {rowCount}
         Requested (UTC): {requestedAt:yyyy-MM-dd HH:mm}

         Download: {stored.DownloadUrl}
         Link expires (UTC): {stored.ExpiresAtUtc:yyyy-MM-dd HH:mm}
         """;

    private static IEnumerable<WaybillReportRow> ToRows(
        Waybill waybill,
        IReadOnlyDictionary<Guid, Product> productsById)
    {
        if (waybill.Items.Count == 0)
        {
            return [ToRow(waybill, null, null)];
        }

        return waybill.Items.Select(item =>
        {
            productsById.TryGetValue(item.ProductId, out Product? product);
            return ToRow(waybill, item, product);
        });
    }

    private static WaybillReportRow ToRow(Waybill waybill, WaybillItem? item, Product? product) => new(
        waybill.TenantId,
        waybill.PackagingDate,
        waybill.WaybillNumber,
        waybill.Status,
        waybill.CancellationReason,
        waybill.PackedAt,
        waybill.HandedOffAt,
        waybill.CancelledAt,
        waybill.CreatedAt,
        waybill.CreatedBy,
        product?.Barcode,
        product?.Name,
        item?.Quantity);

    private static (string ContentType, string Extension) GetFileInfo(WaybillExportFormat format) =>
        format switch
        {
            WaybillExportFormat.Csv => ("text/csv", ".csv"),
            WaybillExportFormat.Xlsx =>
                ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"),
            WaybillExportFormat.Pdf => ("application/pdf", ".pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
}
