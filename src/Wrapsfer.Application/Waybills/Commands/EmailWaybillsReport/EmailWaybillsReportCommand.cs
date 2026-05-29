using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Commands.EmailWaybillsReport;

public sealed record EmailWaybillsReportCommand(
    WaybillExportFormat Format,
    IReadOnlyList<string> RecipientEmails,
    DateOnly? From,
    DateOnly? To,
    WaybillStatus? Status,
    string? Search,
    bool IncludeAllPartnerTenants,
    IReadOnlyList<string>? PartnerTenantIds = null) : ICommand<EmailWaybillsReportResult>;
