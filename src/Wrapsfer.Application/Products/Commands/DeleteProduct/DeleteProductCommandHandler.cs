using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Products.Commands.DeleteProduct;

internal sealed class DeleteProductCommandHandler(
    IProductRepository productRepository,
    IProductComponentRepository productComponentRepository,
    IShopeeProductLinkRepository shopeeProductLinkRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteProductCommand>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Product? product = await productRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        bool isUnpackTarget = await productRepository.IsReferencedAsUnpackTargetAsync(product.Id, tenantId,
            cancellationToken);
        if (isUnpackTarget)
        {
            return Result.Failure(ProductErrors.ProductReferencedAsUnpackTarget);
        }

        bool isLinkedToShopee = await shopeeProductLinkRepository.ExistsForProductAsync(
            tenantId, product.Id, cancellationToken);
        if (isLinkedToShopee)
        {
            return Result.Failure(ProductErrors.LinkedToShopee);
        }

        await productComponentRepository.RemoveAllByParentIdAsync(product.Id, tenantId, cancellationToken);

        IReadOnlyList<ProductComponent> childComponents =
            await productComponentRepository.GetByChildIdAsync(product.Id, tenantId, cancellationToken);
        foreach (ProductComponent component in childComponents)
        {
            productComponentRepository.Remove(component);
        }

        productRepository.Remove(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
