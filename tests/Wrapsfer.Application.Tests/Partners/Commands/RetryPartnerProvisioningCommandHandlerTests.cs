using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Partners.Commands.RetryPartnerProvisioning;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Tests.Partners.Commands;

public class RetryPartnerProvisioningCommandHandlerTests
{
    private const string OwnerRealm = "wrapsfer";

    private readonly RetryPartnerProvisioningCommandHandler _handler;
    private readonly Mock<IIdentityProviderSettings> _identitySettings = new();
    private readonly Mock<IPartnerTenantRepository> _partnerRepository = new();
    private readonly Mock<IIdentityTenantProvisioningService> _provisioningService = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public RetryPartnerProvisioningCommandHandlerTests()
    {
        _identitySettings.Setup(x => x.OwnerRealm).Returns(OwnerRealm);
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSettingsEntity?)null);

        _handler = new RetryPartnerProvisioningCommandHandler(
            _partnerRepository.Object,
            _tenantSettingsRepository.Object,
            _provisioningService.Object,
            _identitySettings.Object,
            _unitOfWork.Object,
            NullLogger<RetryPartnerProvisioningCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenProvisioningSucceeds_UpdatesPartnerProfileAndForwardsDisplayName()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Old Acme", OwnerRealm).Value;
        partner.MarkProvisioningFailed("previous failure");
        _partnerRepository.Setup(r => r.GetByTenantIdAsync("partner-acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
        _provisioningService.Setup(p => p.CreatePartnerRealmAsync(
                It.IsAny<PartnerRealmProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerRealmProvisioningResult(true));

        RetryPartnerProvisioningCommand command = new(
            "partner-acme",
            " New Acme ",
            "admin@acme.example",
            "acmeadmin",
            "TempPass1234!",
            true,
            " ops@acme.example ");

        Result<PartnerResponse> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("New Acme");
        result.Value.ContactEmail.Should().Be("ops@acme.example");
        result.Value.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Active);

        _provisioningService.Verify(p => p.CreatePartnerRealmAsync(
            It.Is<PartnerRealmProvisioningRequest>(req =>
                req.TenantId == "partner-acme" &&
                req.DisplayName == "New Acme" &&
                req.AdminUsername == "acmeadmin" &&
                req.AdminEmail == "admin@acme.example"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
