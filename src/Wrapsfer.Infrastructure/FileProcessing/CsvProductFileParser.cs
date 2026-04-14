using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Products.Commands.ImportProducts;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class CsvProductFileParser : IProductFileParser
{
    public Task<ProductImportBatch> ParseAsync(Stream stream, ProductFileFormat format,
        CancellationToken cancellationToken = default)
    {
        List<ProductImportRow> rows = [];
        List<RowError> errors = [];

        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null
        };

        using StreamReader reader = new(stream, leaveOpen: true);
        using CsvReader csv = new(reader, config);

        if (!csv.Read())
        {
            return Task.FromResult(new ProductImportBatch(rows, errors));
        }

        csv.ReadHeader();

        string[] header = csv.HeaderRecord ?? [];
        HashSet<string> headerSet = new(header, StringComparer.OrdinalIgnoreCase);

        foreach (string required in new[]
                 {
                     ProductFileColumns.Barcode, ProductFileColumns.Name, ProductFileColumns.Type,
                     ProductFileColumns.Cost, ProductFileColumns.StockQuantity
                 })
        {
            if (!headerSet.Contains(required))
            {
                errors.Add(new RowError(1, required, null, ProductImportMessages.MissingColumn(required)));
            }
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ProductImportBatch(rows, errors));
        }

        int rowNumber = 1;
        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            rows.Add(new ProductImportRow(
                rowNumber,
                ReadField(csv, ProductFileColumns.Barcode),
                ReadField(csv, ProductFileColumns.Name),
                ReadField(csv, ProductFileColumns.SkuCode),
                ReadField(csv, ProductFileColumns.Type),
                ReadField(csv, ProductFileColumns.Cost),
                ReadField(csv, ProductFileColumns.StockQuantity),
                ReadField(csv, ProductFileColumns.LowStockThreshold),
                ReadField(csv, ProductFileColumns.UnpackTargetBarcode),
                ReadField(csv, ProductFileColumns.UnpackQuantityPerPackage),
                ReadField(csv, ProductFileColumns.Components)));
        }

        return Task.FromResult(new ProductImportBatch(rows, errors));
    }

    private static string? ReadField(CsvReader csv, string column)
    {
        if (!csv.TryGetField(column, out string? value))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
