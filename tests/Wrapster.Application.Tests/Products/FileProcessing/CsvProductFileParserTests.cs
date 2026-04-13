using System.Text;
using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Infrastructure.FileProcessing;

namespace Wrapster.Application.Tests.Products.FileProcessing;

public class CsvProductFileParserTests
{
    private readonly CsvProductFileParser _parser = new();

    private static Stream FromString(string csv) => new MemoryStream(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task Parse_WhenHeaderMissingRequiredColumn_ReturnsParseError()
    {
        string csv = "barcode,name,type,stockQuantity\nBC1,Widget,Single,5\n";

        ProductImportBatch batch = await _parser.ParseAsync(FromString(csv), ProductFileFormat.Csv);

        batch.ParseErrors.Should().ContainSingle(e => e.Column == "cost");
        batch.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Parse_WhenValidRows_ParsesAllFields()
    {
        string csv =
            "barcode,name,skuCode,type,cost,stockQuantity,lowStockThreshold,unpackTargetBarcode,unpackQuantityPerPackage,components\n" +
            "BC1,Widget,SKU-1,Single,9.99,100,10,,,\n" +
            "BC2,Kit,,Bundle,19.99,0,,,,BC1:2\n" +
            "BC3,Case,,Package,49.99,5,,BC1,12,\n";

        ProductImportBatch batch = await _parser.ParseAsync(FromString(csv), ProductFileFormat.Csv);

        batch.ParseErrors.Should().BeEmpty();
        batch.Rows.Should().HaveCount(3);
        batch.Rows[0].Barcode.Should().Be("BC1");
        batch.Rows[0].RowNumber.Should().Be(2);
        batch.Rows[1].Components.Should().Be("BC1:2");
        batch.Rows[2].UnpackTargetBarcode.Should().Be("BC1");
    }
}
