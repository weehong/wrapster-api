using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.PartnerIntegrations.Commands.CreatePartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Options;
using Wrapsfer.Application.PartnerIntegrations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PartnerIntegrations.Commands;

public class CreatePartnerIntegrationCredentialCommandHandlerTests
{
    private const string OwnerRealm = "wrapsfer";
    private const string TenantId = "partner-acme";
    private const string ClientId = "wrapsfer-partner-acme-integration";
    private const string ClientUuid = "9f8d7c6b-5a49-4838-9271-605948372615";
    private const string ClientSecret = "generated-secret";
    private const string TokenUrl = "http://kc/realms/partner-acme/protocol/openid-connect/token";
    private const string ApiBaseUrl = "https://api.wrapsfer.com";

    private readonly Mock<IPartnerIntegrationCredentialRepository> _credentialRepository = new();
    private readonly CreatePartnerIntegrationCredentialCommandHandler _handler;
    private readonly Mock<IIdentityProviderSettings> _identitySettings = new();
    private readonly Mock<IPartnerTenantRepository> _partnerRepository = new();
    private readonly Mock<IIntegrationClientProvisioningService> _provisioningService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public CreatePartnerIntegrationCredentialCommandHandlerTests()
    {
        _identitySettings.Setup(x => x.OwnerRealm).Returns(OwnerRealm);
        _identitySettings.Setup(x => x.GetTokenUrl(TenantId)).Returns(TokenUrl);
        _handler = new CreatePartnerIntegrationCredentialCommandHandler(
            _partnerRepository.Object,
            _credentialRepository.Object,
            _provisioningService.Object,
            _identitySettings.Object,
            Options.Create(new PartnerIntegrationOptions { ApiBaseUrl = ApiBaseUrl }),
            _unitOfWork.Object,
            NullLogger<CreatePartnerIntegrationCredentialCommandHandler>.Instance);
    }

    private void SetUpExistingPartner()
    {
        PartnerTenant partner = PartnerTenant.Create(TenantId, "Acme", OwnerRealm).Value;
        partner.MarkActive();
        _partnerRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
    }

    private void SetUpSuccessfulProvisioning()
    {
        _provisioningService.Setup(p => p.CreateIntegrationClientAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrationClientProvisioningResult(
                true, null, ClientUuid, ClientId, ClientSecret));
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsMetadataAndSecretOnce()
    {
        SetUpExistingPartner();
        SetUpSuccessfulProvisioning();

        Result<CreatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new CreatePartnerIntegrationCredentialCommand(TenantId, "Acme Integration"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClientSecret.Should().Be(ClientSecret);
        result.Value.Credential.TenantId.Should().Be(TenantId);
        result.Value.Credential.ClientId.Should().Be(ClientId);
        result.Value.Credential.TokenUrl.Should().Be(TokenUrl);
        result.Value.Credential.ApiBaseUrl.Should().Be(ApiBaseUrl);
        result.Value.Credential.IsEnabled.Should().BeTrue();
        _credentialRepository.Verify(r => r.Add(It.Is<PartnerIntegrationCredential>(
            c => c.TenantId == TenantId && c.ClientId == ClientId && c.KeycloakClientUuid == ClientUuid)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDisplayNameOmitted_DefaultsToPartnerDisplayName()
    {
        SetUpExistingPartner();
        SetUpSuccessfulProvisioning();

        Result<CreatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new CreatePartnerIntegrationCredentialCommand(TenantId, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Credential.DisplayName.Should().Be("Acme Integration");
    }

    [Fact]
    public async Task Handle_WhenTenantIdIsOwnerRealm_ReturnsOwnerRealmNotAllowed()
    {
        Result<CreatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new CreatePartnerIntegrationCredentialCommand(OwnerRealm, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.OwnerRealmNotAllowed.Code);
        _provisioningService.Verify(
            p => p.CreateIntegrationClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPartnerMissing_ReturnsPartnerTenantNotFound()
    {
        _partnerRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerTenant?)null);

        Result<CreatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new CreatePartnerIntegrationCredentialCommand(TenantId, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.PartnerTenantNotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenCredentialAlreadyExists_ReturnsConflictWithoutProvisioning()
    {
        SetUpExistingPartner();
        _credentialRepository.Setup(r => r.ExistsForTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<CreatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new CreatePartnerIntegrationCredentialCommand(TenantId, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.AlreadyExists.Code);
        _provisioningService.Verify(
            p => p.CreateIntegrationClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProvisioningFails_ReturnsIdentityProviderFailureAndDoesNotSave()
    {
        SetUpExistingPartner();
        _provisioningService.Setup(p => p.CreateIntegrationClientAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrationClientProvisioningResult(false, "kc-down"));

        Result<CreatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new CreatePartnerIntegrationCredentialCommand(TenantId, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.IdentityProviderFailure.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMetadataSaveFails_CleansUpIdentityClientAndReturnsCreationFailed()
    {
        SetUpExistingPartner();
        SetUpSuccessfulProvisioning();
        _provisioningService.Setup(p => p.DeleteIntegrationClientAsync(
                TenantId, ClientUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrationClientProvisioningResult(true));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unique violation"));

        Result<CreatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new CreatePartnerIntegrationCredentialCommand(TenantId, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.CreationFailed.Code);
        _provisioningService.Verify(
            p => p.DeleteIntegrationClientAsync(TenantId, ClientUuid, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
