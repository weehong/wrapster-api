using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class CompositeWaybillReportFileWriter(
    CsvWaybillReportFileWriter csvWriter,
    ExcelWaybillReportFileWriter excelWriter,
    PdfWaybillReportFileWriter pdfWriter) : IWaybillReportFileWriter
{
    public Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillExportFormat format,
        CancellationToken cancellationToken = default) =>
        format switch
        {
            WaybillExportFormat.Csv => csvWriter.WriteAsync(rows, format, cancellationToken),
            WaybillExportFormat.Xlsx => excelWriter.WriteAsync(rows, format, cancellationToken),
            WaybillExportFormat.Pdf => pdfWriter.WriteAsync(rows, format, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
}
