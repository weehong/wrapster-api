namespace Wrapsfer.Application.Waybills.Commands.EmailWaybillsReport;

public sealed record EmailWaybillsReportResult(WaybillExportDeliveryMode DeliveryMode, int RowCount);
