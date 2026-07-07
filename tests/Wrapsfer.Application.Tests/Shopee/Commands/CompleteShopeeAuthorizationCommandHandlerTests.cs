using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Commands.CompleteShopeeAuthorization;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Commands;

public class CompleteShopeeAuthorizationCommandHandlerTests
{
    private const string OwnerRealm = "wrapsfer";
    private const string TenantId = "partner-acme";
    private const long ShopId = 123456;
    private const string Code = "auth-code";
    private const string Username = "admin@acme";

    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly CompleteShopeeAuthorizationCommandHandler _handler;
    private readonly Mock<IPartnerTenantRepository> _partnerRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public CompleteShopeeAuthorizationCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.Username).Returns(Username);
        _handler = new CompleteShopeeAuthorizationCommandHandler(
            _partnerRepository.Object,
            _connectionRepository.Object,
            _gateway.Object,
            _tenantContext.Object,
            _unitOfWork.Object,
            NullLogger<CompleteShopeeAuthorizationCommandHandler>.Instance);
    }

    private void SetUpExistingPartner()
    {
        PartnerTenant partner = PartnerTenant.Create(TenantId, "Acme", OwnerRealm).Value;
        _partnerRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
    }

    private void SetUpSuccessfulExchange()
    {
        _gateway.Setup(g => g.ExchangeAuthorizationCodeAsync(Code, ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeTokenGrant>.Success(
                new ShopeeTokenGrant("access-token", "refresh-token", 14400)));
    }

    [Fact]
    public async Task Handle_WhenValid_PersistsConnectionWithProfile()
    {
        SetUpExistingPartner();
        SetUpSuccessfulExchange();
        _gateway.Setup(g => g.GetShopProfileAsync(ShopId, "access-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeShopProfile>.Success(new ShopeeShopProfile("Acme Store", "SG")));

        Result<ShopeeConnectionResponse> result = await _handler.Handle(
            new CompleteShopeeAuthorizationCommand(TenantId, Code, ShopId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShopId.Should().Be(ShopId);
        result.Value.ShopName.Should().Be("Acme Store");
        result.Value.Region.Should().Be("SG");
        _connectionRepository.Verify(r => r.Add(It.Is<ShopeeShopConnection>(
            c => c.TenantId == TenantId
                 && c.ShopId == ShopId
                 && c.AccessToken == "access-token"
                 && c.RefreshToken == "refresh-token"
                 && c.LinkedBy == Username)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProfileFetchFails_StillPersistsConnection()
    {
        SetUpExistingPartner();
        SetUpSuccessfulExchange();
        _gateway.Setup(g => g.GetShopProfileAsync(ShopId, "access-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeShopProfile>.Failure(ShopeeShopConnectionErrors.ShopProfileFetchFailed));

        Result<ShopeeConnectionResponse> result = await _handler.Handle(
            new CompleteShopeeAuthorizationCommand(TenantId, Code, ShopId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShopName.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPartnerMissing_Fails()
    {
        Result<ShopeeConnectionResponse> result = await _handler.Handle(
            new CompleteShopeeAuthorizationCommand(TenantId, Code, ShopId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.PartnerTenantNotFound);
        _gateway.Verify(
            g => g.ExchangeAuthorizationCodeAsync(
                It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAlreadyLinked_FailsWithConflict()
    {
        SetUpExistingPartner();
        _connectionRepository.Setup(r => r.ExistsForTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<ShopeeConnectionResponse> result = await _handler.Handle(
            new CompleteShopeeAuthorizationCommand(TenantId, Code, ShopId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.AlreadyLinked);
        _gateway.Verify(
            g => g.ExchangeAuthorizationCodeAsync(
                It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenExchangeFails_PropagatesErrorWithoutPersisting()
    {
        SetUpExistingPartner();
        _gateway.Setup(g => g.ExchangeAuthorizationCodeAsync(Code, ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeTokenGrant>.Failure(
                ShopeeShopConnectionErrors.AuthorizationExchangeFailed));

        Result<ShopeeConnectionResponse> result = await _handler.Handle(
            new CompleteShopeeAuthorizationCommand(TenantId, Code, ShopId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.AuthorizationExchangeFailed);
        _connectionRepository.Verify(r => r.Add(It.IsAny<ShopeeShopConnection>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
