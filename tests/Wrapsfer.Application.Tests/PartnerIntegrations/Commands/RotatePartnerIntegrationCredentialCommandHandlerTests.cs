using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.PartnerIntegrations.Commands.RotatePartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Options;
using Wrapsfer.Application.PartnerIntegrations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PartnerIntegrations.Commands;

public class RotatePartnerIntegrationCredentialCommandHandlerTests
{
    private const string TenantId = "partner-acme";
    private const string ClientUuid = "9f8d7c6b-5a49-4838-9271-605948372615";
    private const string NewSecret = "rotated-secret";

    private readonly Mock<IPartnerIntegrationCredentialRepository> _credentialRepository = new();
    private readonly RotatePartnerIntegrationCredentialCommandHandler _handler;
    private readonly Mock<IIdentityProviderSettings> _identitySettings = new();
    private readonly Mock<IIntegrationClientProvisioningService> _provisioningService = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public RotatePartnerIntegrationCredentialCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.Username).Returns("owner-admin");
        _handler = new RotatePartnerIntegrationCredentialCommandHandler(
            _credentialRepository.Object,
            _provisioningService.Object,
            _identitySettings.Object,
            _tenantContext.Object,
            Options.Create(new PartnerIntegrationOptions()),
            _unitOfWork.Object,
            NullLogger<RotatePartnerIntegrationCredentialCommandHandler>.Instance);
    }

    private static PartnerIntegrationCredential CreateCredential() =>
        PartnerIntegrationCredential.Create(
            TenantId, "wrapsfer-partner-acme-integration", ClientUuid, "Acme Integration").Value;

    private void SetUpExistingCredential(PartnerIntegrationCredential credential) =>
        _credentialRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credential);

    [Fact]
    public async Task Handle_WhenValid_ReturnsNewSecretAndMarksRotated()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        SetUpExistingCredential(credential);
        _provisioningService.Setup(p => p.RotateIntegrationClientSecretAsync(
                TenantId, ClientUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrationClientProvisioningResult(
                true, ClientUuid: ClientUuid, ClientSecret: NewSecret));

        Result<RotatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new RotatePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClientSecret.Should().Be(NewSecret);
        credential.LastRotatedAt.Should().NotBeNull();
        credential.LastRotatedBy.Should().Be("owner-admin");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCredentialMissing_ReturnsNotFound()
    {
        _credentialRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerIntegrationCredential?)null);

        Result<RotatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new RotatePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenCredentialDisabled_ReturnsDisabledWithoutRotating()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        credential.Disable(DateTime.UtcNow, "owner-admin");
        SetUpExistingCredential(credential);

        Result<RotatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new RotatePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.Disabled.Code);
        _provisioningService.Verify(p => p.RotateIntegrationClientSecretAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRotationFails_ReturnsIdentityProviderFailureAndDoesNotSave()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        SetUpExistingCredential(credential);
        _provisioningService.Setup(p => p.RotateIntegrationClientSecretAsync(
                TenantId, ClientUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrationClientProvisioningResult(false, "kc-down"));

        Result<RotatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new RotatePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.IdentityProviderFailure.Code);
        credential.LastRotatedAt.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMetadataSaveFails_StillReturnsNewSecret()
    {
        PartnerIntegrationCredential credential = CreateCredential();
        SetUpExistingCredential(credential);
        _provisioningService.Setup(p => p.RotateIntegrationClientSecretAsync(
                TenantId, ClientUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrationClientProvisioningResult(
                true, ClientUuid: ClientUuid, ClientSecret: NewSecret));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        Result<RotatePartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new RotatePartnerIntegrationCredentialCommand(TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClientSecret.Should().Be(NewSecret);
    }
}
