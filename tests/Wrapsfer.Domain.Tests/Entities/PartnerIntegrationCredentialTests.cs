using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class PartnerIntegrationCredentialTests
{
    private const string TenantId = "partner-acme";
    private const string ClientId = "wrapsfer-partner-acme-integration";
    private const string ClientUuid = "9f8d7c6b-5a49-4838-9271-605948372615";

    private static PartnerIntegrationCredential CreateCredential() =>
        PartnerIntegrationCredential.Create(TenantId, ClientId, ClientUuid, "Acme Integration").Value;

    [Fact]
    public void Create_WithValidInputs_ReturnsEnabledCredential()
    {
        Result<PartnerIntegrationCredential> result =
            PartnerIntegrationCredential.Create(TenantId, ClientId, ClientUuid, "Acme Integration");

        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(TenantId);
        result.Value.ClientId.Should().Be(ClientId);
        result.Value.KeycloakClientUuid.Should().Be(ClientUuid);
        result.Value.DisplayName.Should().Be("Acme Integration");
        result.Value.IsEnabled.Should().BeTrue();
        result.Value.LastRotatedAt.Should().BeNull();
        result.Value.DisabledAt.Should().BeNull();
    }

    [Fact]
    public void Create_TrimsDisplayName()
    {
        Result<PartnerIntegrationCredential> result =
            PartnerIntegrationCredential.Create(TenantId, ClientId, ClientUuid, "  Acme Integration  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Acme Integration");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingDisplayName_ReturnsInvalidDisplayName(string displayName)
    {
        Result<PartnerIntegrationCredential> result =
            PartnerIntegrationCredential.Create(TenantId, ClientId, ClientUuid, displayName);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PartnerIntegrationCredentialErrors.InvalidDisplayName);
    }

    [Fact]
    public void Create_WithTooLongDisplayName_ReturnsInvalidDisplayName()
    {
        Result<PartnerIntegrationCredential> result =
            PartnerIntegrationCredential.Create(TenantId, ClientId, ClientUuid, new string('a', 257));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PartnerIntegrationCredentialErrors.InvalidDisplayName);
    }

    [Fact]
    public void Create_WithMissingTenantId_ReturnsInvalidTenantId()
    {
        Result<PartnerIntegrationCredential> result =
            PartnerIntegrationCredential.Create("", ClientId, ClientUuid, "Acme Integration");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PartnerIntegrationCredentialErrors.InvalidTenantId);
    }

    [Fact]
    public void Create_WithMissingClientId_ReturnsInvalidClientId()
    {
        Result<PartnerIntegrationCredential> result =
            PartnerIntegrationCredential.Create(TenantId, "", ClientUuid, "Acme Integration");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PartnerIntegrationCredentialErrors.InvalidClientId);
    }

    [Fact]
    public void Create_WithMissingKeycloakClientUuid_ReturnsInvalidKeycloakClientUuid()
    {
        Result<PartnerIntegrationCredential> result =
            PartnerIntegrationCredential.Create(TenantId, ClientId, "", "Acme Integration");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PartnerIntegrationCredentialErrors.InvalidKeycloakClientUuid);
    }

    [Fact]
    public void MarkRotated_WhenEnabled_SetsRotationMetadata()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        DateTime rotatedAt = new(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc);

        Result result = credential.MarkRotated(rotatedAt, "owner-admin");

        result.IsSuccess.Should().BeTrue();
        credential.LastRotatedAt.Should().Be(rotatedAt);
        credential.LastRotatedBy.Should().Be("owner-admin");
    }

    [Fact]
    public void MarkRotated_WhenDisabled_ReturnsDisabled()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        credential.Disable(DateTime.UtcNow, "owner-admin");

        Result result = credential.MarkRotated(DateTime.UtcNow, "owner-admin");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PartnerIntegrationCredentialErrors.Disabled);
        credential.LastRotatedAt.Should().BeNull();
    }

    [Fact]
    public void Disable_WhenEnabled_DisablesCredential()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        DateTime disabledAt = new(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc);

        Result result = credential.Disable(disabledAt, "owner-admin");

        result.IsSuccess.Should().BeTrue();
        credential.IsEnabled.Should().BeFalse();
        credential.DisabledAt.Should().Be(disabledAt);
        credential.DisabledBy.Should().Be("owner-admin");
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ReturnsAlreadyDisabled()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        credential.Disable(DateTime.UtcNow, "owner-admin");

        Result result = credential.Disable(DateTime.UtcNow, "owner-admin");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PartnerIntegrationCredentialErrors.AlreadyDisabled);
    }

    [Fact]
    public void Enable_WhenDisabled_EnablesAndClearsDisableMetadata()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        credential.Disable(DateTime.UtcNow, "owner-admin");

        Result result = credential.Enable();

        result.IsSuccess.Should().BeTrue();
        credential.IsEnabled.Should().BeTrue();
        credential.DisabledAt.Should().BeNull();
        credential.DisabledBy.Should().BeNull();
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ReturnsAlreadyEnabled()
    {
        PartnerIntegrationCredential credential = CreateCredential();

        Result result = credential.Enable();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PartnerIntegrationCredentialErrors.AlreadyEnabled);
    }
}
