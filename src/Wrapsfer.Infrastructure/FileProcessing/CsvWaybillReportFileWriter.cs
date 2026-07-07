using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class CsvWaybillReportFileWriter : IWaybillReportFileWriter
{
    public async Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillReportMetadata metadata,
        WaybillExportFormat format, CancellationToken cancellationToken = default)
    {
        WaybillReport report = WaybillReportModelBuilder.Build(rows, metadata);

        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using MemoryStream buffer = new();
        await using (StreamWriter writer = new(buffer, leaveOpen: true))
        await using (CsvWriter csv = new(writer, config))
        {
            await WriteSummarySectionAsync(csv, report.Summary);
            await BlankLineAsync(csv);
            await WriteDailySummarySectionAsync(csv, report.DailySummaries);
            await BlankLineAsync(csv);
            await WriteProductQuantitiesSectionAsync(csv, report.ProductQuantities);
            await BlankLineAsync(csv);
            await WriteDetailsSectionAsync(csv, rows);
        }

        return buffer.ToArray();
    }

    private static async Task WriteSummarySectionAsync(CsvWriter csv, WaybillReportSummary summary)
    {
        await WriteRecordAsync(csv, "Summary");
        await WriteRecordAsync(csv, "Metric", "Value");
        await WriteRecordAsync(csv, "Report Period", summary.ReportPeriod);
        await WriteRecordAsync(csv, "Total Waybill Records",
            summary.TotalWaybillRecords.ToString(CultureInfo.InvariantCulture));
        await WriteRecordAsync(csv, "Total Items Scanned",
            summary.TotalItemsScanned.ToString(CultureInfo.InvariantCulture));
        await WriteRecordAsync(csv, "Unique Products",
            summary.UniqueProducts.ToString(CultureInfo.InvariantCulture));
        await WriteRecordAsync(csv, "Exported By", summary.ExportedBy);
        await WriteRecordAsync(csv, "Generated At",
            summary.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    }

    private static async Task WriteDailySummarySectionAsync(
        CsvWriter csv, IReadOnlyList<WaybillDailySummary> dailySummaries)
    {
        await WriteRecordAsync(csv, "Daily Summary");
        await WriteRecordAsync(csv, "Date", "Waybill Records", "Items Scanned");
        foreach (WaybillDailySummary daily in dailySummaries)
        {
            await WriteRecordAsync(csv,
                daily.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                daily.WaybillRecords.ToString(CultureInfo.InvariantCulture),
                daily.ItemsScanned.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static async Task WriteProductQuantitiesSectionAsync(
        CsvWriter csv, IReadOnlyList<WaybillProductQuantity> productQuantities)
    {
        await WriteRecordAsync(csv, "Total Packed Product Quantities");
        await WriteRecordAsync(csv, "#", "Product Name", "Barcode", "Total Qty");
        foreach (WaybillProductQuantity product in productQuantities)
        {
            await WriteRecordAsync(csv,
                product.Rank.ToString(CultureInfo.InvariantCulture),
                product.ProductName,
                product.Barcode ?? string.Empty,
                product.TotalQuantity.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static async Task WriteDetailsSectionAsync(CsvWriter csv, IReadOnlyList<WaybillReportRow> rows)
    {
        await WriteRecordAsync(csv, "Details");

        foreach (string column in WaybillReportFileColumns.AllInOrder)
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();

        foreach (WaybillReportRow row in rows)
        {
            csv.WriteField(row.TenantId);
            csv.WriteField(row.PackagingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            csv.WriteField(row.WaybillNumber);
            csv.WriteField(row.Status.ToString());
            csv.WriteField(row.CancellationReason ?? string.Empty);
            csv.WriteField(FormatDateTime(row.PackedAt));
            csv.WriteField(FormatDateTime(row.HandedOffAt));
            csv.WriteField(FormatDateTime(row.CancelledAt));
            csv.WriteField(FormatDateTime(row.CreatedAt));
            csv.WriteField(row.CreatedBy ?? string.Empty);
            csv.WriteField(row.ProductBarcode ?? string.Empty);
            csv.WriteField(row.ProductName ?? string.Empty);
            csv.WriteField(row.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            await csv.NextRecordAsync();
        }
    }

    private static async Task WriteRecordAsync(CsvWriter csv, params string[] fields)
    {
        foreach (string field in fields)
        {
            csv.WriteField(field);
        }

        await csv.NextRecordAsync();
    }

    private static async Task BlankLineAsync(CsvWriter csv) => await csv.NextRecordAsync();

    private static string FormatDateTime(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
}
