using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

public sealed record RequestWaybillsExportCommand(
    WaybillExportFormat Format,
    DateOnly? From,
    DateOnly? To,
    WaybillStatus? Status,
    string? Search,
    bool IncludeAllPartnerTenants,
    IReadOnlyList<string>? PartnerTenantIds = null) : ICommand;
