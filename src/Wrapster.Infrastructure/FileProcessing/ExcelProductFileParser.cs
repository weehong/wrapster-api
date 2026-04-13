using ClosedXML.Excel;
using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Application.Products.Commands.ImportProducts;

namespace Wrapster.Infrastructure.FileProcessing;

internal sealed class ExcelProductFileParser : IProductFileParser
{
    private const string ProductsSheetName = "Products";

    public Task<ProductImportBatch> ParseAsync(Stream stream, ProductFileFormat format,
        CancellationToken cancellationToken = default)
    {
        List<ProductImportRow> rows = [];
        List<RowError> errors = [];

        using XLWorkbook workbook = new(stream);

        IXLWorksheet? sheet = workbook.Worksheets
                                  .FirstOrDefault(s =>
                                      string.Equals(s.Name, ProductsSheetName, StringComparison.OrdinalIgnoreCase))
                              ?? workbook.Worksheets.FirstOrDefault();

        if (sheet is null)
        {
            return Task.FromResult(new ProductImportBatch(rows, errors));
        }

        IXLRow headerRow = sheet.FirstRowUsed() ?? sheet.Row(1);
        Dictionary<string, int> columnIndex = new(StringComparer.OrdinalIgnoreCase);

        foreach (IXLCell cell in headerRow.CellsUsed())
        {
            string header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header) && !columnIndex.ContainsKey(header))
            {
                columnIndex[header] = cell.Address.ColumnNumber;
            }
        }

        foreach (string required in new[]
                 {
                     ProductFileColumns.Barcode, ProductFileColumns.Name, ProductFileColumns.Type,
                     ProductFileColumns.Cost, ProductFileColumns.StockQuantity
                 })
        {
            if (!columnIndex.ContainsKey(required))
            {
                errors.Add(new RowError(1, required, null, ProductImportMessages.MissingColumn(required)));
            }
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ProductImportBatch(rows, errors));
        }

        int headerRowNumber = headerRow.RowNumber();
        IXLRange? range = sheet.RangeUsed();
        int lastRow = range?.LastRow().RowNumber() ?? headerRowNumber;

        for (int rowNum = headerRowNumber + 1; rowNum <= lastRow; rowNum++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IXLRow dataRow = sheet.Row(rowNum);
            if (dataRow.IsEmpty())
            {
                continue;
            }

            rows.Add(new ProductImportRow(
                rowNum,
                ReadCell(dataRow, columnIndex, ProductFileColumns.Barcode),
                ReadCell(dataRow, columnIndex, ProductFileColumns.Name),
                ReadCell(dataRow, columnIndex, ProductFileColumns.SkuCode),
                ReadCell(dataRow, columnIndex, ProductFileColumns.Type),
                ReadCell(dataRow, columnIndex, ProductFileColumns.Cost),
                ReadCell(dataRow, columnIndex, ProductFileColumns.StockQuantity),
                ReadCell(dataRow, columnIndex, ProductFileColumns.LowStockThreshold),
                ReadCell(dataRow, columnIndex, ProductFileColumns.UnpackTargetBarcode),
                ReadCell(dataRow, columnIndex, ProductFileColumns.UnpackQuantityPerPackage),
                ReadCell(dataRow, columnIndex, ProductFileColumns.Components)));
        }

        return Task.FromResult(new ProductImportBatch(rows, errors));
    }

    private static string? ReadCell(IXLRow row, Dictionary<string, int> columnIndex, string column)
    {
        if (!columnIndex.TryGetValue(column, out int index))
        {
            return null;
        }

        IXLCell cell = row.Cell(index);
        string value = cell.GetString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
