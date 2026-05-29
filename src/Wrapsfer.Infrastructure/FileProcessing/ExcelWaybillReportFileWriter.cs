using System.Globalization;
using ClosedXML.Excel;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class ExcelWaybillReportFileWriter : IWaybillReportFileWriter
{
    public Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillReportMetadata metadata,
        WaybillExportFormat format, CancellationToken cancellationToken = default)
    {
        WaybillReport report = WaybillReportModelBuilder.Build(rows, metadata);

        using XLWorkbook workbook = new();

        WriteSummarySheet(workbook, report.Summary);
        WriteDailySummarySheet(workbook, report.DailySummaries);
        WriteProductQuantitiesSheet(workbook, report.ProductQuantities);
        WriteDetailsSheet(workbook, rows);

        using MemoryStream buffer = new();
        workbook.SaveAs(buffer);
        return Task.FromResult(buffer.ToArray());
    }

    private static void WriteSummarySheet(XLWorkbook workbook, WaybillReportSummary summary)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add("Summary");
        HeaderCell(sheet.Cell(1, 1), "Metric");
        HeaderCell(sheet.Cell(1, 2), "Value");

        (string Metric, string Value)[] metrics =
        [
            ("Report Period", summary.ReportPeriod),
            ("Total Waybill Records", summary.TotalWaybillRecords.ToString(CultureInfo.InvariantCulture)),
            ("Total Items Scanned", summary.TotalItemsScanned.ToString(CultureInfo.InvariantCulture)),
            ("Unique Products", summary.UniqueProducts.ToString(CultureInfo.InvariantCulture)),
            ("Exported By", summary.ExportedBy),
            ("Generated At", summary.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        ];

        int rowIndex = 2;
        foreach ((string metric, string value) in metrics)
        {
            sheet.Cell(rowIndex, 1).Value = metric;
            sheet.Cell(rowIndex, 2).Value = value;
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteDailySummarySheet(
        XLWorkbook workbook, IReadOnlyList<WaybillDailySummary> dailySummaries)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add("Daily Summary");
        HeaderCell(sheet.Cell(1, 1), "Date");
        HeaderCell(sheet.Cell(1, 2), "Waybill Records");
        HeaderCell(sheet.Cell(1, 3), "Items Scanned");

        int rowIndex = 2;
        foreach (WaybillDailySummary daily in dailySummaries)
        {
            sheet.Cell(rowIndex, 1).Value = daily.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            sheet.Cell(rowIndex, 2).Value = daily.WaybillRecords;
            sheet.Cell(rowIndex, 3).Value = daily.ItemsScanned;
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteProductQuantitiesSheet(
        XLWorkbook workbook, IReadOnlyList<WaybillProductQuantity> productQuantities)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add("Product Quantities");
        HeaderCell(sheet.Cell(1, 1), "#");
        HeaderCell(sheet.Cell(1, 2), "Product Name");
        HeaderCell(sheet.Cell(1, 3), "Barcode");
        HeaderCell(sheet.Cell(1, 4), "Total Qty");

        int rowIndex = 2;
        foreach (WaybillProductQuantity product in productQuantities)
        {
            sheet.Cell(rowIndex, 1).Value = product.Rank;
            sheet.Cell(rowIndex, 2).Value = product.ProductName;
            sheet.Cell(rowIndex, 3).Value = product.Barcode ?? string.Empty;
            sheet.Cell(rowIndex, 4).Value = product.TotalQuantity;
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteDetailsSheet(XLWorkbook workbook, IReadOnlyList<WaybillReportRow> rows)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add("Waybill Report");

        for (int col = 0; col < WaybillReportFileColumns.AllInOrder.Count; col++)
        {
            HeaderCell(sheet.Cell(1, col + 1), WaybillReportFileColumns.AllInOrder[col]);
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
    }

    private static void HeaderCell(IXLCell cell, string value)
    {
        cell.Value = value;
        cell.Style.Font.Bold = true;
    }
}
