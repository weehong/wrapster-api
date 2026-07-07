using System.Text;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Infrastructure.FileProcessing;

namespace Wrapsfer.Application.Tests.Products.FileProcessing;

public class ProductStockReportFileWriterTests
{
    private static readonly IReadOnlyList<ProductStockReportRow> s_rows =
    [
        new("BC-1", "SKU-1", "Alpha", 4, 2.50m, 10.00m),
        new("BC-2", null, "Beta", 10, 1.00m, 10.00m)
    ];

    private static readonly ProductStockReportMetadata s_metadata =
        new(new DateOnly(2026, 6, 20), "Accountant", new DateTime(2026, 6, 23, 9, 0, 0, DateTimeKind.Utc), 2, 14, 20.00m);

    [Fact]
    public async Task Excel_ProducesNonEmptyXlsx()
    {
        ExcelProductStockReportFileWriter writer = new();

        byte[] bytes = await writer.WriteAsync(s_rows, s_metadata, ProductStockReportFormat.Xlsx);

        bytes.Should().NotBeEmpty();
        // XLSX is a ZIP archive: the local file header magic is "PK".
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');
    }

    [Fact]
    public async Task Pdf_ProducesNonEmptyPdf()
    {
        PdfProductStockReportFileWriter writer = new();

        byte[] bytes = await writer.WriteAsync(s_rows, s_metadata, ProductStockReportFormat.Pdf);

        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Pdf_WithNoRows_StillProducesPdf()
    {
        PdfProductStockReportFileWriter writer = new();
        ProductStockReportMetadata empty = new(new DateOnly(2026, 6, 20), "Accountant", s_metadata.GeneratedAtUtc, 0, 0, 0m);

        byte[] bytes = await writer.WriteAsync([], empty, ProductStockReportFormat.Pdf);

        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }
}
