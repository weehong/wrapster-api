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

    public Task<byte[]> WriteAsync(IReadOnlyList<WaybillReportRow> rows, WaybillReportMetadata metadata,
        WaybillExportFormat format, CancellationToken cancellationToken = default)
    {
        WaybillReport report = WaybillReportModelBuilder.Build(rows, metadata);

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontFamily(FontFamily).FontSize(9));
                page.Header().Element(header => BuildHeader(header, report.Summary));
                page.Content().Element(content => BuildContent(content, report));
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

    private static void BuildHeader(IContainer container, WaybillReportSummary summary)
    {
        container.PaddingBottom(12).Column(column =>
        {
            column.Item().AlignCenter().Text("Waybill Report").FontSize(18).SemiBold();
            if (!string.IsNullOrWhiteSpace(summary.ReportPeriod))
            {
                column.Item().AlignCenter().Text(summary.ReportPeriod)
                    .FontSize(11).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void BuildContent(IContainer container, WaybillReport report)
    {
        container.Column(column =>
        {
            column.Spacing(18);

            column.Item().Element(section => BuildSummarySection(section, report.Summary));
            column.Item().Element(section => BuildDailySummarySection(section, report.DailySummaries));
            column.Item().Element(section => BuildProductQuantitiesSection(section, report.ProductQuantities));
            column.Item().Element(section => BuildDetailsSection(section, report.Details));
        });
    }

    private static void BuildSummarySection(IContainer container, WaybillReportSummary summary)
    {
        container.Column(column =>
        {
            SectionHeading(column, "Summary");
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                });

                HeaderRow(table, "Metric", "Value");

                SummaryRow(table, "Report Period", summary.ReportPeriod);
                SummaryRow(table, "Total Waybill Records",
                    summary.TotalWaybillRecords.ToString(CultureInfo.InvariantCulture));
                SummaryRow(table, "Total Items Scanned",
                    summary.TotalItemsScanned.ToString(CultureInfo.InvariantCulture));
                SummaryRow(table, "Unique Products",
                    summary.UniqueProducts.ToString(CultureInfo.InvariantCulture));
                SummaryRow(table, "Exported By", summary.ExportedBy);
                SummaryRow(table, "Generated At",
                    summary.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            });
        });
    }

    private static void BuildDailySummarySection(
        IContainer container, IReadOnlyList<WaybillDailySummary> dailySummaries)
    {
        container.Column(column =>
        {
            SectionHeading(column, "Daily Summary");
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                });

                HeaderRow(table, "Date", "Waybill Records", "Items Scanned");

                foreach (WaybillDailySummary daily in dailySummaries)
                {
                    BodyCell(table, daily.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    BodyCellRight(table, daily.WaybillRecords.ToString(CultureInfo.InvariantCulture));
                    BodyCellRight(table, daily.ItemsScanned.ToString(CultureInfo.InvariantCulture));
                }
            });
        });
    }

    private static void BuildProductQuantitiesSection(
        IContainer container, IReadOnlyList<WaybillProductQuantity> productQuantities)
    {
        container.Column(column =>
        {
            SectionHeading(column, "Total Packed Product Quantities");
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(28f);
                    columns.RelativeColumn(3f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1f);
                });

                HeaderRow(table, "#", "Product Name", "Barcode", "Total Qty");

                foreach (WaybillProductQuantity product in productQuantities)
                {
                    BodyCell(table, product.Rank.ToString(CultureInfo.InvariantCulture));
                    BodyCell(table, product.ProductName);
                    BodyCell(table, product.Barcode ?? string.Empty);
                    BodyCellRight(table, product.TotalQuantity.ToString(CultureInfo.InvariantCulture));
                }
            });
        });
    }

    private static void BuildDetailsSection(IContainer container, IReadOnlyList<WaybillDetailRow> details)
    {
        container.Column(column =>
        {
            SectionHeading(column, "Details");

            if (details.Count == 0)
            {
                column.Item().PaddingTop(8)
                    .Text("No scanned items matched the selected filters.")
                    .FontColor(Colors.Grey.Darken2);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(32f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(3f);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "#");
                    HeaderCell(header.Cell(), "Date");
                    HeaderCell(header.Cell(), "Time");
                    HeaderCell(header.Cell(), "Waybill");
                    HeaderCell(header.Cell(), "Barcode");
                    HeaderCell(header.Cell(), "Product Name");
                });

                foreach (WaybillDetailRow detail in details)
                {
                    BodyCell(table, detail.Index.ToString(CultureInfo.InvariantCulture));
                    BodyCell(table, detail.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    BodyCell(table, detail.Time?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty);
                    BodyCell(table, detail.WaybillNumber);
                    BodyCell(table, detail.Barcode ?? string.Empty);
                    BodyCell(table, detail.ProductName);
                }
            });
        });
    }

    private static void SectionHeading(ColumnDescriptor column, string title) =>
        column.Item().PaddingBottom(6).Text(title).FontSize(13).SemiBold();

    private static void SummaryRow(TableDescriptor table, string metric, string value)
    {
        BodyCell(table, metric);
        BodyCell(table, value);
    }

    private static void HeaderRow(TableDescriptor table, params string[] headings)
    {
        table.Header(header =>
        {
            foreach (string heading in headings)
            {
                HeaderCell(header.Cell(), heading);
            }
        });
    }

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background(Colors.Grey.Darken3).Padding(5)
            .Text(text).FontColor(Colors.White).SemiBold();

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(5).Text(text);

    private static void BodyCellRight(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(5).AlignRight().Text(text);
}
