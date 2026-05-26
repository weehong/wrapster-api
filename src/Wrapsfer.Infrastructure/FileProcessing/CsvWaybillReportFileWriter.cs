using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class CsvWaybillReportFileWriter : IWaybillReportFileWriter
{
    public async Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillExportFormat format,
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

        return buffer.ToArray();
    }

    private static string FormatDateTime(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
}
