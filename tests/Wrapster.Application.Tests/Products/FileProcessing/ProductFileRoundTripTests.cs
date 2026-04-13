using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Infrastructure.FileProcessing;

namespace Wrapster.Application.Tests.Products.FileProcessing;

public class ProductFileRoundTripTests
{
    private static IReadOnlyList<ProductExportRow> SampleRows() =>
    [
        new("BC1", "Widget", "SKU-1", "Single", 9.99m, 100, 10, null, null, null),
        new("BC2", "Kit", null, "Bundle", 19.99m, 0, null, null, null, "BC1:2;BC3:3"),
        new("BC4", "Case", null, "Package", 49.99m, 5, null, "BC1", 12, null)
    ];

    [Fact]
    public async Task Csv_Roundtrips_Preserves_Fields()
    {
        CsvProductFileWriter writer = new();
        CsvProductFileParser parser = new();

        byte[] bytes = await writer.WriteAsync(SampleRows(), ProductFileFormat.Csv);
        using MemoryStream ms = new(bytes);
        ProductImportBatch batch = await parser.ParseAsync(ms, ProductFileFormat.Csv);

        batch.ParseErrors.Should().BeEmpty();
        batch.Rows.Should().HaveCount(3);
        batch.Rows[0].Barcode.Should().Be("BC1");
        batch.Rows[1].Components.Should().Be("BC1:2;BC3:3");
        batch.Rows[2].UnpackTargetBarcode.Should().Be("BC1");
        batch.Rows[2].UnpackQuantityPerPackage.Should().Be("12");
    }

    [Fact]
    public async Task Xlsx_Roundtrips_Preserves_Fields()
    {
        ExcelProductFileWriter writer = new();
        ExcelProductFileParser parser = new();

        byte[] bytes = await writer.WriteAsync(SampleRows(), ProductFileFormat.Xlsx);
        using MemoryStream ms = new(bytes);
        ProductImportBatch batch = await parser.ParseAsync(ms, ProductFileFormat.Xlsx);

        batch.ParseErrors.Should().BeEmpty();
        batch.Rows.Should().HaveCount(3);
        batch.Rows[1].Type.Should().Be("Bundle");
        batch.Rows[1].Components.Should().Be("BC1:2;BC3:3");
    }
}
