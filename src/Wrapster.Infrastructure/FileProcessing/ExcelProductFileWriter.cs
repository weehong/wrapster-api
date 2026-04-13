using ClosedXML.Excel;
using Wrapster.Application.Abstractions.FileProcessing;

namespace Wrapster.Infrastructure.FileProcessing;

internal sealed class ExcelProductFileWriter : IProductFileWriter
{
    private const string ProductsSheetName = "Products";
    private const string InstructionsSheetName = "Instructions";

    public Task<byte[]> WriteAsync(IReadOnlyList<ProductExportRow> rows, ProductFileFormat format,
        CancellationToken cancellationToken = default)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add(ProductsSheetName);

        for (int col = 0; col < ProductFileColumns.AllInOrder.Count; col++)
        {
            IXLCell cell = sheet.Cell(1, col + 1);
            cell.Value = ProductFileColumns.AllInOrder[col];
            cell.Style.Font.Bold = true;
        }

        int rowIndex = 2;
        foreach (ProductExportRow row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.Barcode;
            sheet.Cell(rowIndex, 2).Value = row.Name;
            sheet.Cell(rowIndex, 3).Value = row.SkuCode ?? string.Empty;
            sheet.Cell(rowIndex, 4).Value = row.Type;
            sheet.Cell(rowIndex, 5).Value = row.Cost;
            sheet.Cell(rowIndex, 6).Value = row.StockQuantity;
            sheet.Cell(rowIndex, 7).Value = row.LowStockThreshold.HasValue
                ? row.LowStockThreshold.Value
                : XLCellValue.FromObject(string.Empty);
            sheet.Cell(rowIndex, 8).Value = row.UnpackTargetBarcode ?? string.Empty;
            sheet.Cell(rowIndex, 9).Value = row.UnpackQuantityPerPackage.HasValue
                ? row.UnpackQuantityPerPackage.Value
                : XLCellValue.FromObject(string.Empty);
            sheet.Cell(rowIndex, 10).Value = row.Components ?? string.Empty;
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();

        AddInstructionsSheet(workbook);

        using MemoryStream buffer = new();
        workbook.SaveAs(buffer);
        return Task.FromResult(buffer.ToArray());
    }

    private static void AddInstructionsSheet(XLWorkbook workbook)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add(InstructionsSheetName);

        (string Title, string Body)[] sections =
        [
            ("How to use this file",
                "Fill in the Products sheet. Each row is one product. Rows with a barcode that already exists in your catalog will be UPDATED — they won't be duplicated. Rows with a new barcode will be CREATED."),
            ("Required columns",
                "barcode, name, type, cost, stockQuantity. The other columns are optional unless noted below."),
            ("type",
                "Use one of: Single, Bundle, or Package. Single is a normal product. Bundle groups several single products together. Package is a multi-pack that can be unpacked into another single product."),
            ("Bundle components",
                "For bundles, fill the 'components' column using this format: childBarcode:quantity;childBarcode:quantity (e.g. 4900000000001:2;4900000000002:3). Each child must already exist and must itself be a Single product."),
            ("Package unpack target",
                "For packages, fill 'unpackTargetBarcode' and 'unpackQuantityPerPackage'. The target must already exist and must be a Single product. A package cannot unpack into itself."),
            ("Numbers",
                "Use digits only for cost and quantities. Decimals are allowed for cost (e.g. 9.99). Stock quantity and thresholds must be whole numbers (0 or greater)."),
            ("Errors",
                "If any row has an error, nothing is saved. We return an errors list with the row number and column so you can fix and re-upload.")
        ];

        sheet.Cell(1, 1).Value = "Wrapster products import — instructions";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        int row = 3;
        foreach ((string title, string body) in sections)
        {
            sheet.Cell(row, 1).Value = title;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row + 1, 1).Value = body;
            sheet.Cell(row + 1, 1).Style.Alignment.WrapText = true;
            sheet.Range(row + 1, 1, row + 1, 1).Merge();
            row += 3;
        }

        sheet.Column(1).Width = 100;
    }
}
