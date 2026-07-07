using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.PartnerIntegrations.Commands.DisablePartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Options;
using Wrapsfer.Application.PartnerIntegrations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PartnerIntegrations.Commands;

public class DisablePartnerIntegrationCredentialCommandHandlerTests
{
    private const string TenantId = "partner-acme";
    private const string ClientUuid = "9f8d7c6b-5a49-4838-9271-605948372615";

    private readonly Mock<IPartnerIntegrationCredentialRepository> _credentialRepository = new();
    private readonly DisablePartnerIntegrationCredentialCommandHandler _handler;
    private readonly Mock<IIdentityProviderSettings> _identitySettings = new();
    private readonly Mock<IIntegrationClientProvisioningService> _provisioningService = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public DisablePartnerIntegrationCredentialCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.Username).Returns("owner-admin");
        _handler = new DisablePartnerIntegrationCredentialCommandHandler(
            _credentialRepository.Object,
            _provisioningService.Object,
            _identitySettings.Object,
            _tenantContext.Object,
            Options.Create(new PartnerIntegrationOptions()),
            _unitOfWork.Object,
            NullLogger<DisablePartnerIntegrationCredentialCommandHandler>.Instance);
    }

    private static PartnerIntegrationCredential CreateCredential() =>
        PartnerIntegrationCredential.Create(
            TenantId, "wrapsfer-partner-acme-integration", ClientUuid, "Acme Integration").Value;

    private void SetUpExistingCredential(PartnerIntegrationCredential credential) =>
        _credentialRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credential);

    [Fact]
    public async Task Handle_WhenValid_DisablesCredentialAndKeycloakClient()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        SetUpExistingCredential(credential);
        _provisioningService.Setup(p => p.SetIntegrationClientEnabledAsync(
                TenantId, ClientUuid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrationClientProvisioningResult(true, ClientUuid: ClientUuid));

        Result<PartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new DisablePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
        credential.IsEnabled.Should().BeFalse();
        credential.DisabledBy.Should().Be("owner-admin");
        _provisioningService.Verify(p => p.SetIntegrationClientEnabledAsync(
            TenantId, ClientUuid, false, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCredentialMissing_ReturnsNotFound()
    {
        _credentialRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerIntegrationCredential?)null);

        Result<PartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new DisablePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenAlreadyDisabled_ReturnsAlreadyDisabled()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        credential.Disable(DateTime.UtcNow, "owner-admin");
        SetUpExistingCredential(credential);

        Result<PartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new DisablePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.AlreadyDisabled.Code);
        _provisioningService.Verify(p => p.SetIntegrationClientEnabledAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenKeycloakRejectsUpdate_ReturnsIdentityProviderFailureAndDoesNotSave()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        SetUpExistingCredential(credential);
        _provisioningService.Setup(p => p.SetIntegrationClientEnabledAsync(
                TenantId, ClientUuid, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrationClientProvisioningResult(false, "kc-down"));

        Result<PartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new DisablePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.IdentityProviderFailure.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
