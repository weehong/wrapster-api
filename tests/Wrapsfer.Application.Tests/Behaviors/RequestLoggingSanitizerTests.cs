using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Auth.Commands.ChangePassword;
using Wrapsfer.Application.Auth.Commands.Login;
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

    [Fact]
    public void Sanitize_WithPositionalRecordSensitiveProperty_RedactsValue()
    {
        SensitivePositionalRecord request = new("admin", "secret123");

        string result = RequestLoggingSanitizer.Sanitize(request);

        result.Should().Contain("***REDACTED***");
        result.Should().Contain("admin");
        result.Should().NotContain("secret123");
    }

    [Fact]
    public void Sanitize_LoginCommand_RedactsPasswordButKeepsRealmAndUsername()
    {
        LoginCommand command = new("partner-a", "operator", "hunter2");

        string result = RequestLoggingSanitizer.Sanitize(command);

        result.Should().NotContain("hunter2");
        result.Should().Contain("***REDACTED***");
        result.Should().Contain("partner-a");
        result.Should().Contain("operator");
    }

    [Fact]
    public void Sanitize_ChangePasswordCommand_RedactsBothPasswords()
    {
        ChangePasswordCommand command = new(
            "partner-a", "user-123", "operator", "oldPass!1", "newPass!2");

        string result = RequestLoggingSanitizer.Sanitize(command);

        result.Should().NotContain("oldPass!1");
        result.Should().NotContain("newPass!2");
        result.Should().Contain("***REDACTED***");
        result.Should().Contain("partner-a");
        result.Should().Contain("user-123");
        result.Should().Contain("operator");
    }

    private class SensitiveRequest
    {
        public string Username { get; set; } = "";

        [SensitiveData] public string Password { get; set; } = "";
    }

    private sealed record SensitivePositionalRecord(
        string Username,
        [property: SensitiveData] string Password);
}
