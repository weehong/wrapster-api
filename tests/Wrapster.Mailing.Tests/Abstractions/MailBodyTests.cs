using Wrapster.Mailing.Abstractions;

namespace Wrapster.Mailing.Tests.Abstractions;

public class MailBodyTests
{
    [Fact]
    public void Constructor_WithBothNull_Throws()
    {
        Action act = () => _ = new MailBody();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithBothWhitespace_Throws()
    {
        Action act = () => _ = new MailBody(" ", "\t");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithHtmlOnly_Succeeds()
    {
        MailBody body = new(html: "<p>hi</p>");
        body.Html.Should().Be("<p>hi</p>");
        body.Text.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithTextOnly_Succeeds()
    {
        MailBody body = MailBody.FromText("hi");
        body.Text.Should().Be("hi");
        body.Html.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithBoth_StoresBoth()
    {
        MailBody body = MailBody.FromBoth("<p>hi</p>", "hi");
        body.Html.Should().Be("<p>hi</p>");
        body.Text.Should().Be("hi");
    }
}
