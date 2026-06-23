using System.Globalization;
using ClosedXML.Excel;
using Wrapsfer.Application.Abstractions.FileProcessing;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class ExcelProductStockReportFileWriter : IProductStockReportFileWriter
{
    private const string CurrencyFormat = "#,##0.00";

    public Task<byte[]> WriteAsync(IReadOnlyList<ProductStockReportRow> rows, ProductStockReportMetadata metadata,
        ProductStockReportFormat format, CancellationToken cancellationToken = default)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Stock Report");

        sheet.Cell(1, 1).Value = "Product Stock Report";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        WriteMetadataRow(sheet, 2, "As Of Date",
            metadata.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        WriteMetadataRow(sheet, 3, "Generated At (UTC)",
            metadata.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        WriteMetadataRow(sheet, 4, "Exported By", metadata.ExportedBy ?? string.Empty);

        sheet.Cell(5, 1).Value = "Total Products";
        sheet.Cell(5, 2).Value = metadata.TotalProducts;
        sheet.Cell(6, 1).Value = "Total Units";
        sheet.Cell(6, 2).Value = metadata.TotalUnits;
        sheet.Cell(7, 1).Value = "Total Inventory Value";
        sheet.Cell(7, 2).Value = metadata.GrandTotalValue;
        sheet.Cell(7, 2).Style.NumberFormat.Format = CurrencyFormat;

        const int headerRow = 9;
        string[] headers = ["Barcode", "SKU", "Product Name", "Quantity", "Unit Cost", "Total Value"];
        for (int col = 0; col < headers.Length; col++)
        {
            IXLCell cell = sheet.Cell(headerRow, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
        }

        int rowIndex = headerRow + 1;
        foreach (ProductStockReportRow row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.Barcode;
            sheet.Cell(rowIndex, 2).Value = row.SkuCode ?? string.Empty;
            sheet.Cell(rowIndex, 3).Value = row.Name;
            sheet.Cell(rowIndex, 4).Value = row.Quantity;
            sheet.Cell(rowIndex, 5).Value = row.UnitCost;
            sheet.Cell(rowIndex, 5).Style.NumberFormat.Format = CurrencyFormat;
            sheet.Cell(rowIndex, 6).Value = row.TotalValue;
            sheet.Cell(rowIndex, 6).Style.NumberFormat.Format = CurrencyFormat;
            rowIndex++;
        }

        sheet.Cell(rowIndex, 3).Value = "Grand Total";
        sheet.Cell(rowIndex, 3).Style.Font.Bold = true;
        sheet.Cell(rowIndex, 4).Value = metadata.TotalUnits;
        sheet.Cell(rowIndex, 4).Style.Font.Bold = true;
        sheet.Cell(rowIndex, 6).Value = metadata.GrandTotalValue;
        sheet.Cell(rowIndex, 6).Style.Font.Bold = true;
        sheet.Cell(rowIndex, 6).Style.NumberFormat.Format = CurrencyFormat;

        sheet.Columns().AdjustToContents();

        using MemoryStream buffer = new();
        workbook.SaveAs(buffer);
        return Task.FromResult(buffer.ToArray());
    }

    private static void WriteMetadataRow(IXLWorksheet sheet, int rowIndex, string label, string value)
    {
        sheet.Cell(rowIndex, 1).Value = label;
        sheet.Cell(rowIndex, 2).Value = value;
    }
}
