using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.DisconnectShopeeShop;

internal sealed class DisconnectShopeeShopCommandHandler(
    IShopeeShopConnectionRepository connectionRepository,
    IUnitOfWork unitOfWork,
    ILogger<DisconnectShopeeShopCommandHandler> logger)
    : ICommandHandler<DisconnectShopeeShopCommand>
{
    public async Task<Result> Handle(
        DisconnectShopeeShopCommand request,
        CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection =
            await connectionRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result.Failure(ShopeeShopConnectionErrors.NotFound);
        }

        connectionRepository.Remove(connection);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Shopee shop {ShopId} unlinked from tenant {TenantId}",
            connection.ShopId, request.TenantId);

        return Result.Success();
    }
}
