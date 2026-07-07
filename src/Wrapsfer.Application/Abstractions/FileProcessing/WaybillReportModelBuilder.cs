using System.Globalization;

namespace Wrapsfer.Application.Abstractions.FileProcessing;

/// <summary>
/// Transforms the flat per-item <see cref="WaybillReportRow"/> list into the structured,
/// format-agnostic <see cref="WaybillReport"/> consumed by the PDF/Excel/CSV writers. All
/// aggregation (daily counts, per-product totals, per-unit detail expansion) lives here so the
/// writers stay pure renderers.
/// </summary>
public static class WaybillReportModelBuilder
{
    private const string UnknownExporter = "Unknown";

    public static WaybillReport Build(IReadOnlyList<WaybillReportRow> rows, WaybillReportMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(metadata);

        WaybillReportSummary summary = BuildSummary(rows, metadata);
        IReadOnlyList<WaybillDailySummary> dailySummaries = BuildDailySummaries(rows);
        IReadOnlyList<WaybillProductQuantity> productQuantities = BuildProductQuantities(rows);
        IReadOnlyList<WaybillDetailRow> details = BuildDetails(rows);

        return new WaybillReport(summary, dailySummaries, productQuantities, details);
    }

    private static WaybillReportSummary BuildSummary(
        IReadOnlyList<WaybillReportRow> rows, WaybillReportMetadata metadata)
    {
        int totalWaybillRecords = rows
            .Select(r => r.WaybillNumber)
            .Distinct(StringComparer.Ordinal)
            .Count();

        int totalItemsScanned = rows.Sum(r => r.Quantity ?? 0);

        int uniqueProducts = rows
            .Where(HasProduct)
            .Select(ProductKey)
            .Distinct(StringComparer.Ordinal)
            .Count();

        string exportedBy = string.IsNullOrWhiteSpace(metadata.ExportedBy)
            ? UnknownExporter
            : metadata.ExportedBy!;

        return new WaybillReportSummary(
            BuildReportPeriod(rows, metadata),
            totalWaybillRecords,
            totalItemsScanned,
            uniqueProducts,
            exportedBy,
            metadata.GeneratedAtUtc);
    }

    private static string BuildReportPeriod(IReadOnlyList<WaybillReportRow> rows, WaybillReportMetadata metadata)
    {
        DateOnly? from = metadata.From;
        DateOnly? to = metadata.To;

        if (from is null && to is null && rows.Count > 0)
        {
            from = rows.Min(r => r.PackagingDate);
            to = rows.Max(r => r.PackagingDate);
        }

        if (from is null && to is null)
        {
            return string.Empty;
        }

        if (from is not null && to is not null)
        {
            return from.Value == to.Value
                ? FormatDate(from.Value)
                : $"{FormatDate(from.Value)} — {FormatDate(to.Value)}";
        }

        return FormatDate((from ?? to)!.Value);
    }

    private static IReadOnlyList<WaybillDailySummary> BuildDailySummaries(IReadOnlyList<WaybillReportRow> rows) =>
        rows
            .GroupBy(r => r.PackagingDate)
            .OrderBy(g => g.Key)
            .Select(g => new WaybillDailySummary(
                g.Key,
                g.Select(r => r.WaybillNumber).Distinct(StringComparer.Ordinal).Count(),
                g.Sum(r => r.Quantity ?? 0)))
            .ToList();

    private static IReadOnlyList<WaybillProductQuantity> BuildProductQuantities(IReadOnlyList<WaybillReportRow> rows) =>
        rows
            .Where(HasProduct)
            .GroupBy(ProductKey)
            .Select(g => new
            {
                Name = g.First().ProductName ?? string.Empty,
                Barcode = g.First().ProductBarcode,
                TotalQuantity = g.Sum(r => r.Quantity ?? 0)
            })
            .OrderByDescending(p => p.TotalQuantity)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .Select((p, index) => new WaybillProductQuantity(index + 1, p.Name, p.Barcode, p.TotalQuantity))
            .ToList();

    private static IReadOnlyList<WaybillDetailRow> BuildDetails(IReadOnlyList<WaybillReportRow> rows)
    {
        List<WaybillDetailRow> details = new();
        int index = 1;

        IEnumerable<WaybillReportRow> ordered = rows
            .Where(r => HasProduct(r) && (r.Quantity ?? 0) > 0)
            .OrderBy(r => r.PackedAt ?? r.CreatedAt)
            .ThenBy(r => r.WaybillNumber, StringComparer.Ordinal)
            .ThenBy(r => r.ProductName, StringComparer.Ordinal);

        foreach (WaybillReportRow row in ordered)
        {
            TimeOnly? time = row.PackedAt is { } packedAt ? TimeOnly.FromDateTime(packedAt) : null;
            int quantity = row.Quantity ?? 0;

            for (int unit = 0; unit < quantity; unit++)
            {
                details.Add(new WaybillDetailRow(
                    index++,
                    row.PackagingDate,
                    time,
                    row.WaybillNumber,
                    row.ProductBarcode,
                    row.ProductName ?? string.Empty));
            }
        }

        return details;
    }

    private static bool HasProduct(WaybillReportRow row) =>
        !string.IsNullOrWhiteSpace(row.ProductBarcode) || !string.IsNullOrWhiteSpace(row.ProductName);

    private static string ProductKey(WaybillReportRow row) =>
        string.IsNullOrWhiteSpace(row.ProductBarcode)
            ? row.ProductName ?? string.Empty
            : row.ProductBarcode!;

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
