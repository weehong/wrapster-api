using ClosedXML.Excel;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class ExcelWaybillReportFileWriter : IWaybillReportFileWriter
{
    public Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillExportFormat format,
        CancellationToken cancellationToken = default)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Waybill Report");

        for (int col = 0; col < WaybillReportFileColumns.AllInOrder.Count; col++)
        {
            IXLCell cell = sheet.Cell(1, col + 1);
            cell.Value = WaybillReportFileColumns.AllInOrder[col];
            cell.Style.Font.Bold = true;
        }

        int rowIndex = 2;
        foreach (WaybillReportRow row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.TenantId;
            sheet.Cell(rowIndex, 2).Value = row.PackagingDate.ToString("yyyy-MM-dd");
            sheet.Cell(rowIndex, 3).Value = row.WaybillNumber;
            sheet.Cell(rowIndex, 4).Value = row.Status.ToString();
            sheet.Cell(rowIndex, 5).Value = row.CancellationReason ?? string.Empty;
            sheet.Cell(rowIndex, 6).Value = row.PackedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            sheet.Cell(rowIndex, 7).Value = row.HandedOffAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            sheet.Cell(rowIndex, 8).Value = row.CancelledAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            sheet.Cell(rowIndex, 9).Value = row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            sheet.Cell(rowIndex, 10).Value = row.CreatedBy ?? string.Empty;
            sheet.Cell(rowIndex, 11).Value = row.ProductBarcode ?? string.Empty;
            sheet.Cell(rowIndex, 12).Value = row.ProductName ?? string.Empty;
            sheet.Cell(rowIndex, 13).Value = row.Quantity.HasValue
                ? row.Quantity.Value
                : XLCellValue.FromObject(string.Empty);
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();

        using MemoryStream buffer = new();
        workbook.SaveAs(buffer);
        return Task.FromResult(buffer.ToArray());
    }
}
