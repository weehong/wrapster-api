using Wrapster.Application.Email.Templates;

namespace Wrapster.Application.Tests.Email.Templates;

public class EmailTemplateTests
{
    [Fact]
    public void LowStockAlertTemplate_Subject_ContainsProductNameAndStock()
    {
        LowStockAlertTemplate template = new("Widget", "BC-001", 3, 10);

        template.Subject.Should().Contain("Widget");
        template.Subject.Should().Contain("3");
    }

    [Fact]
    public void LowStockAlertTemplate_RenderHtml_ContainsProductDetails()
    {
        LowStockAlertTemplate template = new("Widget", "BC-001", 3, 10);

        string html = template.RenderHtml();

        html.Should().Contain("Widget");
        html.Should().Contain("BC-001");
        html.Should().Contain("3");
        html.Should().Contain("10");
    }

    [Fact]
    public void ReportEmailTemplate_Subject_ContainsReportNameAndDateRange()
    {
        ReportEmailTemplate template = new("Sales Report", "Jan 2026", [], "application/pdf");

        template.Subject.Should().Contain("Sales Report");
        template.Subject.Should().Contain("Jan 2026");
    }

    [Fact]
    public void ReportEmailTemplate_RenderHtml_ContainsReportDetails()
    {
        ReportEmailTemplate template = new("Sales Report", "Jan 2026", [], "application/pdf");

        string html = template.RenderHtml();

        html.Should().Contain("Sales Report");
        html.Should().Contain("Jan 2026");
    }

    [Fact]
    public void ReportEmailTemplate_Attachments_ContainsFileWithCorrectExtension()
    {
        ReportEmailTemplate template = new("Report", "Jan", [1, 2, 3], "application/pdf");

        template.Attachments.Should().HaveCount(1);
        template.Attachments[0].FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public void ReportEmailTemplate_WithXlsxContentType_HasXlsxExtension()
    {
        ReportEmailTemplate template = new("Report", "Jan", [],
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        template.Attachments[0].FileName.Should().EndWith(".xlsx");
    }

    [Fact]
    public void ReportEmailTemplate_WithUnknownContentType_HasBinExtension()
    {
        ReportEmailTemplate template = new("Report", "Jan", [], "application/octet-stream");

        template.Attachments[0].FileName.Should().EndWith(".bin");
    }

    [Fact]
    public void StockChangeNotificationTemplate_Subject_ContainsStockChange()
    {
        StockChangeNotificationTemplate template = new("Widget", "BC-001", 50, 30, "user-1");

        template.Subject.Should().Contain("Widget");
        template.Subject.Should().Contain("50");
        template.Subject.Should().Contain("30");
    }

    [Fact]
    public void StockChangeNotificationTemplate_RenderHtml_ContainsAllDetails()
    {
        StockChangeNotificationTemplate template = new("Widget", "BC-001", 50, 30, "user-1");

        string html = template.RenderHtml();

        html.Should().Contain("Widget");
        html.Should().Contain("BC-001");
        html.Should().Contain("50");
        html.Should().Contain("30");
        html.Should().Contain("decreased");
    }

    [Fact]
    public void StockChangeNotificationTemplate_WhenStockIncreased_ShowsIncreasedDirection()
    {
        StockChangeNotificationTemplate template = new("Widget", "BC-001", 10, 50, "user-1");

        string html = template.RenderHtml();

        html.Should().Contain("increased");
    }
}
