using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class PdfWaybillReportFileWriter : IWaybillReportFileWriter
{
    // Noto Sans CJK SC covers Latin + Simplified Chinese + Japanese + Korean from a single
    // family, which removes the need for a fallback chain. The Docker runtime image installs
    // it via the fonts-noto-cjk package; on dev machines fontconfig usually resolves it from
    // /usr/share/fonts/opentype/noto. If the font is missing at runtime, QuestPDF falls back
    // to the SkiaSharp default, which still renders Latin but loses CJK glyphs.
    private const string FontFamily = "Noto Sans CJK SC";

    static PdfWaybillReportFileWriter()
    {
        // Static ctor runs once before the first use of this type — earlier than any
        // QuestPDF.Document.Create call from either the production host or unit tests.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillExportFormat format,
        CancellationToken cancellationToken = default)
    {
        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontFamily(FontFamily).FontSize(9));
                page.Header().Element(BuildHeader);
                page.Content().Element(content => BuildTable(content, rows));
                page.Footer().AlignRight().Text(footer =>
                {
                    footer.Span("Page ");
                    footer.CurrentPageNumber();
                    footer.Span(" / ");
                    footer.TotalPages();
                });
            });
        }).GeneratePdf();

        return Task.FromResult(pdf);
    }

    private static void BuildHeader(IContainer container)
    {
        container.PaddingBottom(12).Column(column =>
        {
            column.Item().Text("Waybill Report").FontSize(16).SemiBold();
            column.Item().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                .FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void BuildTable(IContainer container, IReadOnlyList<WaybillReportRow> rows)
    {
        if (rows.Count == 0)
        {
            container.AlignCenter().PaddingTop(40)
                .Text("No waybills matched the selected filters.")
                .FontColor(Colors.Grey.Darken2);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.4f); // Tenant
                columns.RelativeColumn(1.1f); // Date
                columns.RelativeColumn(1.4f); // Waybill
                columns.RelativeColumn(1.0f); // Status
                columns.RelativeColumn(3.0f); // Product
                columns.RelativeColumn(0.7f); // Qty
            });

            table.Header(header =>
            {
                foreach (string heading in new[] { "Tenant", "Date", "Waybill", "Status", "Product", "Qty" })
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4)
                        .Text(heading).SemiBold();
                }
            });

            foreach (WaybillReportRow row in rows)
            {
                BodyCell(table, row.TenantId);
                BodyCell(table, row.PackagingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                BodyCell(table, row.WaybillNumber);
                BodyCell(table, row.Status.ToString());
                BodyCell(table, BuildProductLabel(row));
                BodyCell(table, row.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            }
        });
    }

    private static string BuildProductLabel(WaybillReportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.ProductName))
        {
            return row.ProductBarcode ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(row.ProductBarcode)
            ? row.ProductName!
            : $"{row.ProductName} ({row.ProductBarcode})";
    }

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(4).Text(text);
}
