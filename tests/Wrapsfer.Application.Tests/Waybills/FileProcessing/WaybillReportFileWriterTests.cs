using ClosedXML.Excel;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Infrastructure.FileProcessing;

namespace Wrapsfer.Application.Tests.Waybills.FileProcessing;

public class WaybillReportFileWriterTests
{
    private static IReadOnlyList<WaybillReportRow> SampleRows() =>
    [
        new("partner-a", new DateOnly(2026, 5, 24), "WB-100", WaybillStatus.Packed, null,
            new DateTime(2026, 5, 24, 10, 0, 0, DateTimeKind.Utc), null, null,
            new DateTime(2026, 5, 24, 9, 0, 0, DateTimeKind.Utc), "user-1", "BC1", "Widget", 3)
    ];

    private static WaybillReportMetadata SampleMetadata() =>
        new(new DateOnly(2026, 5, 24), new DateOnly(2026, 5, 24), "Vernon Wee Hong KOH",
            new DateTime(2026, 5, 25, 0, 52, 47, DateTimeKind.Utc));

    [Fact]
    public async Task Csv_Writes_Summary_Sections_And_Detail_Fields()
    {
        CsvWaybillReportFileWriter writer = new();

        byte[] bytes = await writer.WriteAsync(SampleRows(), SampleMetadata(), WaybillExportFormat.Csv);
        string csv = System.Text.Encoding.UTF8.GetString(bytes);

        // Aggregate sections.
        csv.Should().Contain("Summary");
        csv.Should().Contain("Total Items Scanned");
        csv.Should().Contain("Vernon Wee Hong KOH");
        csv.Should().Contain("Daily Summary");
        csv.Should().Contain("Total Packed Product Quantities");
        // Full detail dump retained.
        csv.Should().Contain("tenantId");
        csv.Should().Contain("partner-a");
        csv.Should().Contain("WB-100");
        csv.Should().Contain("Widget");
    }

    [Fact]
    public async Task Xlsx_Writes_Summary_Sheets_And_Detail_Sheet()
    {
        ExcelWaybillReportFileWriter writer = new();

        byte[] bytes = await writer.WriteAsync(SampleRows(), SampleMetadata(), WaybillExportFormat.Xlsx);
        using MemoryStream stream = new(bytes);
        using XLWorkbook workbook = new(stream);

        // Aggregate worksheets.
        workbook.TryGetWorksheet("Summary", out _).Should().BeTrue();
        workbook.TryGetWorksheet("Daily Summary", out _).Should().BeTrue();
        workbook.TryGetWorksheet("Product Quantities", out _).Should().BeTrue();

        IXLWorksheet summary = workbook.Worksheet("Summary");
        summary.Cell(1, 1).GetString().Should().Be("Metric");
        summary.Cell(2, 1).GetString().Should().Be("Report Period");

        // Full detail dump retained.
        IXLWorksheet sheet = workbook.Worksheet("Waybill Report");
        sheet.Cell(1, 1).GetString().Should().Be("tenantId");
        sheet.Cell(2, 1).GetString().Should().Be("partner-a");
        sheet.Cell(2, 3).GetString().Should().Be("WB-100");
        sheet.Cell(2, 12).GetString().Should().Be("Widget");
    }

    [Fact]
    public async Task Pdf_Writes_Pdf_Document()
    {
        PdfWaybillReportFileWriter writer = new();

        byte[] bytes = await writer.WriteAsync(SampleRows(), SampleMetadata(), WaybillExportFormat.Pdf);

        // QuestPDF compresses content streams by default, so we can't assert on the body text via
        // a substring search. Verify the magic header + EOF marker to confirm a syntactically
        // valid PDF was produced.
        bytes.Length.Should().BeGreaterThan(0);
        string header = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length));
        header.Should().StartWith("%PDF-1.");
        string tail = System.Text.Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 32), Math.Min(32, bytes.Length));
        tail.Should().Contain("%%EOF");
    }
}
