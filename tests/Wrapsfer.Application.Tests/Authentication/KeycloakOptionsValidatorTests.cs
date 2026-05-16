using Microsoft.Extensions.Hosting;

namespace Wrapsfer.Application.Tests.Authentication;

public class KeycloakOptionsValidatorTests
{
    private static readonly KeycloakOptionsValidator DevelopmentValidator =
        CreateValidator(Environments.Development);

    private static readonly KeycloakOptionsValidator ProductionValidator =
        CreateValidator(Environments.Production);

    [Fact]
    public void Validate_WhenAllRequiredValuesPresent_ReturnsSuccess()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "https://id.example.com",
            OwnerRealm = "owner-realm",
            Audience = "wrapsfer"
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBaseUrlMissing_ReturnsFailure()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "",
            OwnerRealm = "owner-realm",
            Audience = "wrapsfer"
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle(f => f.Contains("BaseUrl"));
    }

    [Fact]
    public void Validate_WhenBaseUrlNotAbsoluteUri_ReturnsFailure()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "/relative/path",
            OwnerRealm = "owner-realm",
            Audience = "wrapsfer"
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("valid absolute URI"));
    }

    [Fact]
    public void Validate_WhenOwnerRealmMissing_ReturnsFailure()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "https://id.example.com",
            OwnerRealm = "",
            Audience = "wrapsfer"
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("OwnerRealm"));
    }

    [Fact]
    public void Validate_WhenAudienceMissing_ReturnsFailure()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "https://id.example.com",
            OwnerRealm = "owner-realm",
            Audience = ""
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Audience"));
    }

    [Fact]
    public void Validate_WhenBaseUrlContainsRealmsPath_ReturnsFailure()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "https://keycloak.example.com/realms/wrapsfer",
            OwnerRealm = "wrapsfer",
            Audience = "wrapsfer"
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("/realms/"));
    }

    [Fact]
    public void Validate_InProduction_RejectsHttpBaseUrl()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "http://keycloak.example.com",
            OwnerRealm = "wrapsfer",
            Audience = "wrapsfer",
            RequireHttpsMetadata = true
        };

        ValidateOptionsResult result = ProductionValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("https in Production"));
    }

    [Fact]
    public void Validate_InProduction_RejectsRequireHttpsMetadataFalse()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "https://keycloak.example.com",
            OwnerRealm = "wrapsfer",
            Audience = "wrapsfer",
            RequireHttpsMetadata = false
        };

        ValidateOptionsResult result = ProductionValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("RequireHttpsMetadata must be true"));
    }

    [Theory]
    [InlineData("http://localhost:18080")]
    [InlineData("http://keycloak:8080")]
    [InlineData("http://127.0.0.1:18080")]
    public void Validate_InProduction_RejectsInternalHosts(string baseUrl)
    {
        KeycloakOptions options = new()
        {
            BaseUrl = baseUrl,
            OwnerRealm = "wrapsfer",
            Audience = "wrapsfer",
            RequireHttpsMetadata = true
        };

        ValidateOptionsResult result = ProductionValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("not allowed in Production"));
    }

    [Fact]
    public void Validate_InProduction_AcceptsHttpsPublicHost()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "https://keycloak.example.com",
            OwnerRealm = "wrapsfer",
            Audience = "wrapsfer",
            RequireHttpsMetadata = true
        };

        ValidateOptionsResult result = ProductionValidator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_InDevelopment_AllowsHttpLocalhostWithRequireHttpsMetadataFalse()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "http://localhost:18080",
            OwnerRealm = "wrapsfer",
            Audience = "wrapsfer",
            RequireHttpsMetadata = false
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenPartnerOnboardingEnabledWithoutAdminCredentials_Fails()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "https://id.example.com",
            OwnerRealm = "owner-realm",
            Audience = "wrapsfer",
            PartnerOnboardingEnabled = true
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("AdminClientId"));
        result.Failures.Should().Contain(f => f.Contains("AdminClientSecret"));
        result.Failures.Should().Contain(f => f.Contains("PartnerApiClientSecret"));
    }

    [Fact]
    public void Validate_WhenPartnerOnboardingEnabledWithAdminCredentials_Succeeds()
    {
        KeycloakOptions options = new()
        {
            BaseUrl = "https://id.example.com",
            OwnerRealm = "owner-realm",
            Audience = "wrapsfer",
            PartnerOnboardingEnabled = true,
            AdminClientId = "wrapsfer-admin",
            AdminClientSecret = "secret",
            PartnerApiClientSecret = "partner-secret"
        };

        ValidateOptionsResult result = DevelopmentValidator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    private static KeycloakOptionsValidator CreateValidator(string environmentName)
    {
        Mock<IHostEnvironment> environment = new();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return new KeycloakOptionsValidator(environment.Object);
    }
}
