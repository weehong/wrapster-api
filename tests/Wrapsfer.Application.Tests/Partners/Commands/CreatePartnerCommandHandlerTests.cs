using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Partners.Commands.CreatePartner;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Tests.Partners.Commands;

public class CreatePartnerCommandHandlerTests
{
    private const string OwnerRealm = "owner";

    private readonly CreatePartnerCommandHandler _handler;
    private readonly Mock<IIdentityProviderSettings> _identitySettings = new();
    private readonly Mock<IPartnerTenantRepository> _partnerRepository = new();
    private readonly Mock<IIdentityTenantProvisioningService> _provisioningService = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public CreatePartnerCommandHandlerTests()
    {
        _identitySettings.Setup(x => x.OwnerRealm).Returns(OwnerRealm);
        _partnerRepository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSettingsEntity?)null);

        _handler = new CreatePartnerCommandHandler(
            _partnerRepository.Object,
            _tenantSettingsRepository.Object,
            _provisioningService.Object,
            _identitySettings.Object,
            _unitOfWork.Object,
            NullLogger<CreatePartnerCommandHandler>.Instance);
    }

    private static CreatePartnerCommand SampleCommand(string tenantId = "partner-acme") =>
        new(tenantId, "Acme", "admin@acme.example", "acmeadmin", "TempPass1234!");

    [Fact]
    public async Task Handle_WhenTenantIdMatchesOwnerRealm_ReturnsOwnerRealmNotAllowed()
    {
        Result<PartnerResponse> result =
            await _handler.Handle(SampleCommand(OwnerRealm), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.OwnerRealmNotAllowed.Code);
        _provisioningService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenPartnerAlreadyExists_ReturnsConflict()
    {
        _partnerRepository.Setup(r => r.ExistsAsync("partner-acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<PartnerResponse> result =
            await _handler.Handle(SampleCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.AlreadyExists.Code);
        _provisioningService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenProvisioningSucceeds_PersistsActivePartnerAndTenantSettings()
    {
        _provisioningService.Setup(p => p.CreatePartnerRealmAsync(
                It.IsAny<PartnerRealmProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerRealmProvisioningResult(true));

        PartnerTenant? captured = null;
        _partnerRepository.Setup(r => r.Add(It.IsAny<PartnerTenant>()))
            .Callback<PartnerTenant>(p => captured = p);

        TenantSettingsEntity? capturedSettings = null;
        _tenantSettingsRepository.Setup(r => r.Add(It.IsAny<TenantSettingsEntity>()))
            .Callback<TenantSettingsEntity>(s => capturedSettings = s);

        Result<PartnerResponse> result = await _handler.Handle(SampleCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be("partner-acme");
        result.Value.IsActive.Should().BeTrue();
        result.Value.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Active);

        captured.Should().NotBeNull();
        captured!.IsActive.Should().BeTrue();
        capturedSettings.Should().NotBeNull();
        capturedSettings!.TenantId.Should().Be("partner-acme");

        _provisioningService.Verify(p => p.CreatePartnerRealmAsync(
            It.Is<PartnerRealmProvisioningRequest>(req =>
                req.TenantId == "partner-acme" &&
                req.AdminUsername == "acmeadmin" &&
                req.AdminEmail == "admin@acme.example"),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task Handle_WhenProvisioningFails_MarksFailedAndReturnsProvisioningFailed()
    {
        _provisioningService.Setup(p => p.CreatePartnerRealmAsync(
                It.IsAny<PartnerRealmProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerRealmProvisioningResult(false, "kc-down"));

        PartnerTenant? captured = null;
        _partnerRepository.Setup(r => r.Add(It.IsAny<PartnerTenant>()))
            .Callback<PartnerTenant>(p => captured = p);

        Result<PartnerResponse> result = await _handler.Handle(SampleCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.ProvisioningFailed.Code);
        captured.Should().NotBeNull();
        captured!.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Failed);
        captured.IsActive.Should().BeFalse();
        _tenantSettingsRepository.Verify(r => r.Add(It.IsAny<TenantSettingsEntity>()), Times.Never);
    }
}
