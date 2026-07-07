using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Queries.GetShopeeAuthorizationLink;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Queries;

public class GetShopeeAuthorizationLinkQueryHandlerTests
{
    private const string OwnerRealm = "wrapsfer";
    private const string TenantId = "partner-acme";
    private const string RedirectUrl = "https://partner.wrapsfer.com/partner/shopee-callback";
    private const string AuthorizationUrl =
        "https://partner.shopeemobile.com/api/v2/shop/auth_partner?partner_id=1&timestamp=2&sign=3&redirect=4";

    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly GetShopeeAuthorizationLinkQueryHandler _handler;
    private readonly Mock<IPartnerTenantRepository> _partnerRepository = new();

    public GetShopeeAuthorizationLinkQueryHandlerTests() =>
        _handler = new GetShopeeAuthorizationLinkQueryHandler(
            _partnerRepository.Object,
            _gateway.Object);

    private void SetUpExistingPartner()
    {
        PartnerTenant partner = PartnerTenant.Create(TenantId, "Acme", OwnerRealm).Value;
        _partnerRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsAuthorizationUrl()
    {
        SetUpExistingPartner();
        _gateway.Setup(g => g.BuildShopAuthorizationUrl(RedirectUrl))
            .Returns(Result<string>.Success(AuthorizationUrl));

        Result<ShopeeAuthorizationLinkResponse> result = await _handler.Handle(
            new GetShopeeAuthorizationLinkQuery(TenantId, RedirectUrl), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorizationUrl.Should().Be(AuthorizationUrl);
    }

    [Fact]
    public async Task Handle_WhenPartnerMissing_Fails()
    {
        Result<ShopeeAuthorizationLinkResponse> result = await _handler.Handle(
            new GetShopeeAuthorizationLinkQuery(TenantId, RedirectUrl), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.PartnerTenantNotFound);
        _gateway.Verify(g => g.BuildShopAuthorizationUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenGatewayNotConfigured_PropagatesError()
    {
        SetUpExistingPartner();
        _gateway.Setup(g => g.BuildShopAuthorizationUrl(RedirectUrl))
            .Returns(Result<string>.Failure(ShopeeShopConnectionErrors.NotConfigured));

        Result<ShopeeAuthorizationLinkResponse> result = await _handler.Handle(
            new GetShopeeAuthorizationLinkQuery(TenantId, RedirectUrl), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.NotConfigured);
    }
}
