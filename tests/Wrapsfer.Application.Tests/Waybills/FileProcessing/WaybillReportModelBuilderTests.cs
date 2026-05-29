using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Tests.Waybills.FileProcessing;

public class WaybillReportModelBuilderTests
{
    private static readonly DateOnly s_date = new(2026, 1, 20);

    private static WaybillReportRow Row(string waybill, string barcode, string product, int qty, int hour) =>
        new("tenant-a", s_date, waybill, WaybillStatus.Packed, null,
            new DateTime(2026, 1, 20, hour, 0, 0, DateTimeKind.Utc), null, null,
            new DateTime(2026, 1, 20, hour, 0, 0, DateTimeKind.Utc), "user-1", barcode, product, qty);

    private static IReadOnlyList<WaybillReportRow> SampleRows() =>
    [
        Row("WB-1", "BC-A", "Alpha", 5, 2),
        Row("WB-1", "BC-B", "Bravo", 1, 2),
        Row("WB-2", "BC-A", "Alpha", 3, 4)
    ];

    private static WaybillReportMetadata Metadata(string? exportedBy = "Vernon Wee Hong KOH") =>
        new(s_date, s_date, exportedBy, new DateTime(2026, 1, 21, 0, 52, 47, DateTimeKind.Utc));

    [Fact]
    public void Build_Computes_Summary_Totals()
    {
        WaybillReport report = WaybillReportModelBuilder.Build(SampleRows(), Metadata());

        report.Summary.TotalWaybillRecords.Should().Be(2);
        report.Summary.TotalItemsScanned.Should().Be(9);
        report.Summary.UniqueProducts.Should().Be(2);
        report.Summary.ReportPeriod.Should().Be("2026-01-20");
        report.Summary.ExportedBy.Should().Be("Vernon Wee Hong KOH");
        report.Summary.GeneratedAtUtc.Should().Be(new DateTime(2026, 1, 21, 0, 52, 47, DateTimeKind.Utc));
    }

    [Fact]
    public void Build_Falls_Back_To_Unknown_Exporter()
    {
        WaybillReport report = WaybillReportModelBuilder.Build(SampleRows(), Metadata(exportedBy: null));

        report.Summary.ExportedBy.Should().Be("Unknown");
    }

    [Fact]
    public void Build_Groups_Daily_Summaries()
    {
        WaybillReport report = WaybillReportModelBuilder.Build(SampleRows(), Metadata());

        report.DailySummaries.Should().ContainSingle();
        WaybillDailySummary daily = report.DailySummaries[0];
        daily.Date.Should().Be(s_date);
        daily.WaybillRecords.Should().Be(2);
        daily.ItemsScanned.Should().Be(9);
    }

    [Fact]
    public void Build_Ranks_Product_Quantities_Descending()
    {
        WaybillReport report = WaybillReportModelBuilder.Build(SampleRows(), Metadata());

        report.ProductQuantities.Should().HaveCount(2);
        report.ProductQuantities[0].Rank.Should().Be(1);
        report.ProductQuantities[0].ProductName.Should().Be("Alpha");
        report.ProductQuantities[0].Barcode.Should().Be("BC-A");
        report.ProductQuantities[0].TotalQuantity.Should().Be(8);
        report.ProductQuantities[1].ProductName.Should().Be("Bravo");
        report.ProductQuantities[1].TotalQuantity.Should().Be(1);
    }

    [Fact]
    public void Build_Expands_Details_By_Quantity()
    {
        WaybillReport report = WaybillReportModelBuilder.Build(SampleRows(), Metadata());

        report.Details.Should().HaveCount(9);
        report.Details[0].Index.Should().Be(1);
        report.Details[^1].Index.Should().Be(9);
        report.Details.Count(d => d.ProductName == "Alpha").Should().Be(8);
        report.Details.Should().OnlyContain(d => d.Time == new TimeOnly(d.Time!.Value.Hour, 0, 0));
    }

    [Fact]
    public void Build_Derives_Report_Period_From_Rows_When_Range_Absent()
    {
        WaybillReportMetadata metadata = new(
            From: null, To: null, ExportedBy: "x",
            GeneratedAtUtc: new DateTime(2026, 1, 21, 0, 0, 0, DateTimeKind.Utc));

        WaybillReport report = WaybillReportModelBuilder.Build(SampleRows(), metadata);

        report.Summary.ReportPeriod.Should().Be("2026-01-20");
    }
}
