using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Products.Commands.SetProductActive;

internal sealed class SetProductActiveCommandHandler(
    IProductRepository productRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<SetProductActiveCommand>
{
    public async Task<Result> Handle(SetProductActiveCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Product? product = await productRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        Result transition = request.IsActive
            ? product.Reactivate()
            : product.Deactivate(DateTime.UtcNow);

        if (transition.IsFailure)
        {
            return transition;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
