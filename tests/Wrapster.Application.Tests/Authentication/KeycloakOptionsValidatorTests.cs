using Microsoft.Extensions.Options;

namespace Wrapster.Application.Tests.Authentication;

public class KeycloakOptionsValidatorTests
{
    private static readonly KeycloakOptionsValidator Validator = new();

    [Fact]
    public void Validate_WhenAllRequiredValuesPresent_ReturnsSuccess()
    {
        var options = new KeycloakOptions
        {
            BaseUrl = "https://id.example.com",
            OwnerRealm = "owner-realm",
            Audience = "wrapster-api"
        };

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBaseUrlMissing_ReturnsFailure()
    {
        var options = new KeycloakOptions
        {
            BaseUrl = "",
            OwnerRealm = "owner-realm",
            Audience = "wrapster-api"
        };

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle(f => f.Contains("BaseUrl"));
    }

    [Fact]
    public void Validate_WhenBaseUrlNotAbsoluteUri_ReturnsFailure()
    {
        var options = new KeycloakOptions
        {
            BaseUrl = "/relative/path",
            OwnerRealm = "owner-realm",
            Audience = "wrapster-api"
        };

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("valid absolute URI"));
    }

    [Fact]
    public void Validate_WhenOwnerRealmMissing_ReturnsFailure()
    {
        var options = new KeycloakOptions
        {
            BaseUrl = "https://id.example.com",
            OwnerRealm = "",
            Audience = "wrapster-api"
        };

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("OwnerRealm"));
    }

    [Fact]
    public void Validate_WhenAudienceMissing_ReturnsFailure()
    {
        var options = new KeycloakOptions
        {
            BaseUrl = "https://id.example.com",
            OwnerRealm = "owner-realm",
            Audience = ""
        };

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Audience"));
    }
}
