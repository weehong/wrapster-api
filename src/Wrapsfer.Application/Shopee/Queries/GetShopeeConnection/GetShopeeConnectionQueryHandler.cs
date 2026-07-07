using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeConnection;

internal sealed class GetShopeeConnectionQueryHandler(
    IShopeeShopConnectionRepository connectionRepository)
    : IQueryHandler<GetShopeeConnectionQuery, ShopeeConnectionResponse>
{
    public async Task<Result<ShopeeConnectionResponse>> Handle(
        GetShopeeConnectionQuery request,
        CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection =
            await connectionRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeConnectionResponse>.Failure(ShopeeShopConnectionErrors.NotFound);
        }

        return Result<ShopeeConnectionResponse>.Success(ShopeeConnectionResponseMapper.Map(connection));
    }
}
