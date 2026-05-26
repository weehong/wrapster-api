using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

public static class WaybillExportErrors
{
    public static readonly Error PartnerScopeNotAllowed = new(
        "WaybillExport.PartnerScopeNotAllowed",
        "Only owner accounts may target specific partner tenants for export.",
        ErrorType.Validation);

    public static readonly Error UnknownOrInactivePartner = new(
        "WaybillExport.UnknownOrInactivePartner",
        "One or more of the requested partner tenants do not exist or are not active.",
        ErrorType.Validation);
}
