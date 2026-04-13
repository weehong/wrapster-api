using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Wrapster.Application.Abstractions.FileProcessing;

namespace Wrapster.Infrastructure.FileProcessing;

internal sealed class CsvProductFileWriter : IProductFileWriter
{
    public async Task<byte[]> WriteAsync(IReadOnlyList<ProductExportRow> rows, ProductFileFormat format,
        CancellationToken cancellationToken = default)
    {
        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using MemoryStream buffer = new();
        await using (StreamWriter writer = new(buffer, leaveOpen: true))
        await using (CsvWriter csv = new(writer, config))
        {
            foreach (string column in ProductFileColumns.AllInOrder)
            {
                csv.WriteField(column);
            }

            await csv.NextRecordAsync();

            foreach (ProductExportRow row in rows)
            {
                csv.WriteField(row.Barcode);
                csv.WriteField(row.Name);
                csv.WriteField(row.SkuCode ?? string.Empty);
                csv.WriteField(row.Type);
                csv.WriteField(row.Cost.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(row.StockQuantity.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(row.LowStockThreshold?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                csv.WriteField(row.UnpackTargetBarcode ?? string.Empty);
                csv.WriteField(row.UnpackQuantityPerPackage?.ToString(CultureInfo.InvariantCulture) ??
                               string.Empty);
                csv.WriteField(row.Components ?? string.Empty);
                await csv.NextRecordAsync();
            }
        }

        return buffer.ToArray();
    }
}
