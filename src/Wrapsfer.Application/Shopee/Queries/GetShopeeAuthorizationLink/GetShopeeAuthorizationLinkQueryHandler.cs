using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeAuthorizationLink;

internal sealed class GetShopeeAuthorizationLinkQueryHandler(
    IPartnerTenantRepository partnerTenantRepository,
    IShopeeGateway shopeeGateway)
    : IQueryHandler<GetShopeeAuthorizationLinkQuery, ShopeeAuthorizationLinkResponse>
{
    public async Task<Result<ShopeeAuthorizationLinkResponse>> Handle(
        GetShopeeAuthorizationLinkQuery request,
        CancellationToken cancellationToken)
    {
        PartnerTenant? partner =
            await partnerTenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (partner is null)
        {
            return Result<ShopeeAuthorizationLinkResponse>.Failure(
                ShopeeShopConnectionErrors.PartnerTenantNotFound);
        }

        Result<string> urlResult = shopeeGateway.BuildShopAuthorizationUrl(request.RedirectUrl);
        if (urlResult.IsFailure)
        {
            return Result<ShopeeAuthorizationLinkResponse>.Failure(urlResult.Error);
        }

        return Result<ShopeeAuthorizationLinkResponse>.Success(
            new ShopeeAuthorizationLinkResponse(urlResult.Value));
    }
}
