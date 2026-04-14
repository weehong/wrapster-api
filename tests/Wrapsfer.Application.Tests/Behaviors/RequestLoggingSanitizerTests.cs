using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Behaviors;

namespace Wrapsfer.Application.Tests.Behaviors;

public class RequestLoggingSanitizerTests
{
    [Fact]
    public void Sanitize_WithNull_ReturnsNullJson()
    {
        string result = RequestLoggingSanitizer.Sanitize(null);

        result.Should().Be("null");
    }

    [Fact]
    public void Sanitize_WithSimpleObject_ReturnsJsonProperties()
    {
        var request = new { Name = "Test", Count = 5 };

        string result = RequestLoggingSanitizer.Sanitize(request);

        result.Should().Contain("\"Name\":\"Test\"");
        result.Should().Contain("\"Count\":5");
    }

    [Fact]
    public void Sanitize_WithSensitiveProperty_RedactsValue()
    {
        SensitiveRequest request = new() { Password = "secret123", Username = "admin" };

        string result = RequestLoggingSanitizer.Sanitize(request);

        result.Should().Contain("***REDACTED***");
        result.Should().Contain("admin");
        result.Should().NotContain("secret123");
    }

    [Fact]
    public void Sanitize_WithEnumerable_SerializesItems()
    {
        List<string> items = ["a", "b", "c"];

        string result = RequestLoggingSanitizer.Sanitize(items);

        result.Should().Contain("\"a\"");
        result.Should().Contain("\"b\"");
    }

    [Fact]
    public void Sanitize_WithGuid_SerializesAsSimpleType()
    {
        Guid id = Guid.NewGuid();
        var request = new { Id = id };

        string result = RequestLoggingSanitizer.Sanitize(request);

        result.Should().Contain(id.ToString());
    }

    private class SensitiveRequest
    {
        public string Username { get; set; } = "";

        [SensitiveData] public string Password { get; set; } = "";
    }
}
