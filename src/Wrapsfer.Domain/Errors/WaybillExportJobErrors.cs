using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class WaybillExportJobErrors
{
    public static readonly Error NotFound = new(
        "WaybillExportJob.NotFound",
        "The waybill export job was not found.",
        ErrorType.NotFound);

    public static readonly Error NotOwned = new(
        "WaybillExportJob.NotOwned",
        "The waybill export job belongs to another user.",
        ErrorType.NotFound);

    public static readonly Error CannotRetryNonFailed = new(
        "WaybillExportJob.CannotRetryNonFailed",
        "Only failed export jobs can be retried.",
        ErrorType.Conflict);

    public static readonly Error CannotDeleteNonTerminal = new(
        "WaybillExportJob.CannotDeleteNonTerminal",
        "Only completed or failed export jobs can be deleted.",
        ErrorType.Conflict);
}
