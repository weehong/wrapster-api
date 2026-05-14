using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Partners.Commands.SetPartnerActive;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Partners.Commands;

public class SetPartnerActiveCommandHandlerTests
{
    private const string OwnerRealm = "owner";

    private readonly SetPartnerActiveCommandHandler _handler;
    private readonly Mock<IIdentityProviderSettings> _identitySettings = new();
    private readonly Mock<IPartnerTenantRepository> _partnerRepository = new();
    private readonly Mock<IIdentityTenantProvisioningService> _provisioningService = new();
    private readonly Mock<IRealmConfigurationCache> _realmCache = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public SetPartnerActiveCommandHandlerTests()
    {
        _identitySettings.Setup(x => x.OwnerRealm).Returns(OwnerRealm);
        _handler = new SetPartnerActiveCommandHandler(
            _partnerRepository.Object,
            _provisioningService.Object,
            _identitySettings.Object,
            _realmCache.Object,
            _unitOfWork.Object,
            NullLogger<SetPartnerActiveCommandHandler>.Instance);
    }

    private static PartnerTenant CreateActivePartner(string tenantId = "partner-acme")
    {
        PartnerTenant partner = PartnerTenant.Create(tenantId, "Acme", OwnerRealm).Value;
        partner.MarkActive();
        return partner;
    }

    [Fact]
    public async Task Handle_WhenTenantIdIsOwnerRealm_ReturnsOwnerRealmNotAllowed()
    {
        Result<PartnerResponse> result =
            await _handler.Handle(new SetPartnerActiveCommand(OwnerRealm, false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.OwnerRealmNotAllowed.Code);
    }

    [Fact]
    public async Task Handle_WhenPartnerMissing_ReturnsNotFound()
    {
        _partnerRepository.Setup(r => r.GetByTenantIdAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerTenant?)null);

        Result<PartnerResponse> result =
            await _handler.Handle(new SetPartnerActiveCommand("ghost", false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenDeactivating_DisablesRealmAndSavesAndClearsCache()
    {
        PartnerTenant partner = CreateActivePartner();
        _partnerRepository.Setup(r => r.GetByTenantIdAsync("partner-acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
        _provisioningService.Setup(p => p.SetPartnerRealmActiveAsync(
                "partner-acme", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerRealmProvisioningResult(true));

        Result<PartnerResponse> result =
            await _handler.Handle(new SetPartnerActiveCommand("partner-acme", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        partner.IsActive.Should().BeFalse();
        partner.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Deactivated);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realmCache.Verify(c => c.Remove("partner-acme"), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenKeycloakRejectsUpdate_ReturnsProvisioningFailedAndDoesNotSave()
    {
        PartnerTenant partner = CreateActivePartner();
        _partnerRepository.Setup(r => r.GetByTenantIdAsync("partner-acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
        _provisioningService.Setup(p => p.SetPartnerRealmActiveAsync(
                "partner-acme", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerRealmProvisioningResult(false, "kc-down"));

        Result<PartnerResponse> result =
            await _handler.Handle(new SetPartnerActiveCommand("partner-acme", false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.ProvisioningFailed.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realmCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Never);
    }
}
