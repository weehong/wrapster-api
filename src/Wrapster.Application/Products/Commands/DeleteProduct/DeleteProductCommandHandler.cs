using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Products.Commands.DeleteProduct;

internal sealed class DeleteProductCommandHandler(
    IProductRepository productRepository,
    IProductComponentRepository productComponentRepository,
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
