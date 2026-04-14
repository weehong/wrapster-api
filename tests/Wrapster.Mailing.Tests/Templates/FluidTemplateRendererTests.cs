using Wrapster.Mailing.Templates;

namespace Wrapster.Mailing.Tests.Templates;

public class FluidTemplateRendererTests
{
    private readonly FluidTemplateRenderer _renderer = new();

    [Fact]
    public async Task RenderAsync_UnknownTemplate_ReturnsNull()
    {
        RenderedTemplate? result = await _renderer.RenderAsync("no-such-template", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RenderAsync_LowStockAlert_RendersSubjectAndBodies()
    {
        Dictionary<string, object?> tokens = new()
        {
            ["ProductName"] = "Widget",
            ["Barcode"] = "BC-001",
            ["CurrentStock"] = 3,
            ["Threshold"] = 10
        };

        RenderedTemplate? result = await _renderer.RenderAsync("low-stock-alert", tokens);

        result.Should().NotBeNull();
        result!.Subject.Should().Be("Low Stock Alert: Widget (3 remaining)");
        result.Html.Should().NotBeNullOrEmpty();
        result.Text.Should().NotBeNullOrEmpty();
        result.Html.Should().Contain("Widget");
        result.Text.Should().Contain("BC-001");
    }

    [Fact]
    public async Task RenderAsync_HtmlContext_EncodesDangerousValues()
    {
        Dictionary<string, object?> tokens = new()
        {
            ["ProductName"] = "<script>alert(1)</script>",
            ["Barcode"] = "BC",
            ["CurrentStock"] = 0,
            ["Threshold"] = 1
        };

        RenderedTemplate? result = await _renderer.RenderAsync("low-stock-alert", tokens);

        result.Should().NotBeNull();
        result!.Html.Should().NotContain("<script>alert(1)</script>");
        result.Html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public async Task RenderAsync_TextContext_KeepsValuesRaw()
    {
        Dictionary<string, object?> tokens = new()
        {
            ["ProductName"] = "Widget & Gadget",
            ["Barcode"] = "BC",
            ["CurrentStock"] = 0,
            ["Threshold"] = 1
        };

        RenderedTemplate? result = await _renderer.RenderAsync("low-stock-alert", tokens);

        result.Should().NotBeNull();
        result!.Text.Should().Contain("Widget & Gadget");
    }
}
