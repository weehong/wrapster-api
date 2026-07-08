using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.UnlinkShopeeProduct;

internal sealed class UnlinkShopeeProductCommandHandler(
    IShopeeProductLinkRepository linkRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UnlinkShopeeProductCommand>
{
    public async Task<Result> Handle(
        UnlinkShopeeProductCommand request,
        CancellationToken cancellationToken)
    {
        ShopeeProductLink? link = await linkRepository.GetByIdAsync(
            request.LinkId, request.TenantId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(ShopeeProductLinkErrors.NotFound);
        }

        linkRepository.Remove(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
