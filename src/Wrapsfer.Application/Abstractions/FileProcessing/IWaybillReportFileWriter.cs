using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Application.Abstractions.FileProcessing;

public interface IWaybillReportFileWriter
{
    Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillReportMetadata metadata,
        WaybillExportFormat format, CancellationToken cancellationToken = default);
}
