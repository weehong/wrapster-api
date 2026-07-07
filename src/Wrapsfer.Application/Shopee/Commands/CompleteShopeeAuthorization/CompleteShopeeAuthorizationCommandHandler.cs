using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.CompleteShopeeAuthorization;

internal sealed class CompleteShopeeAuthorizationCommandHandler(
    IPartnerTenantRepository partnerTenantRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeGateway shopeeGateway,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    ILogger<CompleteShopeeAuthorizationCommandHandler> logger)
    : ICommandHandler<CompleteShopeeAuthorizationCommand, ShopeeConnectionResponse>
{
    public async Task<Result<ShopeeConnectionResponse>> Handle(
        CompleteShopeeAuthorizationCommand request,
        CancellationToken cancellationToken)
    {
        PartnerTenant? partner =
            await partnerTenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (partner is null)
        {
            return Result<ShopeeConnectionResponse>.Failure(
                ShopeeShopConnectionErrors.PartnerTenantNotFound);
        }

        bool exists = await connectionRepository.ExistsForTenantAsync(request.TenantId, cancellationToken);
        if (exists)
        {
            return Result<ShopeeConnectionResponse>.Failure(ShopeeShopConnectionErrors.AlreadyLinked);
        }

        Result<ShopeeTokenGrant> grantResult = await shopeeGateway.ExchangeAuthorizationCodeAsync(
            request.Code, request.ShopId, cancellationToken);
        if (grantResult.IsFailure)
        {
            return Result<ShopeeConnectionResponse>.Failure(grantResult.Error);
        }

        ShopeeTokenGrant grant = grantResult.Value;
        DateTime now = DateTime.UtcNow;

        Result<ShopeeShopConnection> createResult = ShopeeShopConnection.Create(
            request.TenantId,
            request.ShopId,
            grant.AccessToken,
            grant.RefreshToken,
            now.AddSeconds(grant.ExpiresInSeconds),
            now.Add(ShopeeShopConnection.RefreshTokenLifetime),
            now,
            tenantContext.Username);

        if (createResult.IsFailure)
        {
            return Result<ShopeeConnectionResponse>.Failure(createResult.Error);
        }

        ShopeeShopConnection connection = createResult.Value;

        Result<ShopeeShopProfile> profileResult = await shopeeGateway.GetShopProfileAsync(
            request.ShopId, grant.AccessToken, cancellationToken);
        if (profileResult.IsSuccess)
        {
            connection.UpdateShopProfile(profileResult.Value.ShopName, profileResult.Value.Region);
        }
        else
        {
            // The link is still valid without display metadata; the profile fetch is best-effort.
            logger.LogWarning(
                "Shopee shop profile fetch failed for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
                request.TenantId, request.ShopId, profileResult.Error.Code);
        }

        connectionRepository.Add(connection);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ShopeeConnectionResponse>.Success(ShopeeConnectionResponseMapper.Map(connection));
    }
}
