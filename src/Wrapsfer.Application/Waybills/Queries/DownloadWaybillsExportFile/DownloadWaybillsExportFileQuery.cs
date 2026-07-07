using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Queries.DownloadWaybillsExportFile;

public sealed record DownloadWaybillsExportFileQuery(
    WaybillExportFormat Format,
    DateOnly? From,
    DateOnly? To,
    WaybillStatus? Status,
    string? Search,
    bool IncludeAllPartnerTenants,
    IReadOnlyList<string>? PartnerTenantIds = null) : IQuery<DownloadWaybillsExportFileResult>;
