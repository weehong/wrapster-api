using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Wrapsfer.Application.Abstractions.FileProcessing;

namespace Wrapsfer.Infrastructure.FileProcessing;

internal sealed class PdfProductStockReportFileWriter : IProductStockReportFileWriter
{
    // See PdfWaybillReportFileWriter for the rationale behind this font choice.
    private const string FontFamily = "Noto Sans CJK SC";
    private const string CurrencyFormat = "#,##0.00";

    static PdfProductStockReportFileWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> WriteAsync(IReadOnlyList<ProductStockReportRow> rows, ProductStockReportMetadata metadata,
        ProductStockReportFormat format, CancellationToken cancellationToken = default)
    {
        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontFamily(FontFamily).FontSize(9));
                page.Header().Element(header => BuildHeader(header, metadata));
                page.Content().Element(content => BuildContent(content, rows, metadata));
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

    private static void BuildHeader(IContainer container, ProductStockReportMetadata metadata)
    {
        container.PaddingBottom(12).Column(column =>
        {
            column.Item().AlignCenter().Text("Product Stock Report").FontSize(18).SemiBold();
            column.Item().AlignCenter()
                .Text($"As of {metadata.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}")
                .FontSize(11).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void BuildContent(
        IContainer container, IReadOnlyList<ProductStockReportRow> rows, ProductStockReportMetadata metadata)
    {
        container.Column(column =>
        {
            column.Spacing(18);
            column.Item().Element(section => BuildSummarySection(section, metadata));
            column.Item().Element(section => BuildDetailsSection(section, rows, metadata));
        });
    }

    private static void BuildSummarySection(IContainer container, ProductStockReportMetadata metadata)
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
                SummaryRow(table, "As Of Date",
                    metadata.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                SummaryRow(table, "Generated At (UTC)",
                    metadata.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                SummaryRow(table, "Exported By", metadata.ExportedBy ?? string.Empty);
                SummaryRow(table, "Total Products",
                    metadata.TotalProducts.ToString(CultureInfo.InvariantCulture));
                SummaryRow(table, "Total Units",
                    metadata.TotalUnits.ToString(CultureInfo.InvariantCulture));
                SummaryRow(table, "Total Inventory Value",
                    metadata.GrandTotalValue.ToString(CurrencyFormat, CultureInfo.InvariantCulture));
            });
        });
    }

    private static void BuildDetailsSection(
        IContainer container, IReadOnlyList<ProductStockReportRow> rows, ProductStockReportMetadata metadata)
    {
        container.Column(column =>
        {
            SectionHeading(column, "Stock");

            if (rows.Count == 0)
            {
                column.Item().PaddingTop(8)
                    .Text("No stock-holding products as of the selected date.")
                    .FontColor(Colors.Grey.Darken2);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(3f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.4f);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Barcode");
                    HeaderCell(header.Cell(), "SKU");
                    HeaderCell(header.Cell(), "Product Name");
                    HeaderCellRight(header.Cell(), "Qty");
                    HeaderCellRight(header.Cell(), "Unit Cost");
                    HeaderCellRight(header.Cell(), "Total Value");
                });

                foreach (ProductStockReportRow row in rows)
                {
                    BodyCell(table, row.Barcode);
                    BodyCell(table, row.SkuCode ?? string.Empty);
                    BodyCell(table, row.Name);
                    BodyCellRight(table, row.Quantity.ToString(CultureInfo.InvariantCulture));
                    BodyCellRight(table, row.UnitCost.ToString(CurrencyFormat, CultureInfo.InvariantCulture));
                    BodyCellRight(table, row.TotalValue.ToString(CurrencyFormat, CultureInfo.InvariantCulture));
                }

                table.Cell().ColumnSpan(3).BorderTop(1).Padding(5).Text("Grand Total").SemiBold();
                table.Cell().BorderTop(1).Padding(5).AlignRight()
                    .Text(metadata.TotalUnits.ToString(CultureInfo.InvariantCulture)).SemiBold();
                table.Cell().BorderTop(1).Padding(5);
                table.Cell().BorderTop(1).Padding(5).AlignRight()
                    .Text(metadata.GrandTotalValue.ToString(CurrencyFormat, CultureInfo.InvariantCulture)).SemiBold();
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

    private static void HeaderCellRight(IContainer cell, string text) =>
        cell.Background(Colors.Grey.Darken3).Padding(5).AlignRight()
            .Text(text).FontColor(Colors.White).SemiBold();

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(5).Text(text);

    private static void BodyCellRight(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(5).AlignRight().Text(text);
}
