using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Mailing.Tests.Abstractions;

public class MailRequestIdTests
{
    [Fact]
    public void New_ReturnsDistinctValues()
    {
        MailRequestId a = MailRequestId.New();
        MailRequestId b = MailRequestId.New();

        a.Should().NotBe(b);
        a.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        Guid guid = Guid.NewGuid();
        MailRequestId id = new(guid);

        id.ToString().Should().Be(guid.ToString());
    }
}
