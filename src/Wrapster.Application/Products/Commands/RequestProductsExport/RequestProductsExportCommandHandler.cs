using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Abstractions.Queue;
using Wrapster.Application.Products.Messaging;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Products.Commands.RequestProductsExport;

internal sealed class RequestProductsExportCommandHandler(
    IMessagePublisher messagePublisher,
    ITenantContext tenantContext) : ICommandHandler<RequestProductsExportCommand>
{
    public async Task<Result> Handle(RequestProductsExportCommand request, CancellationToken cancellationToken)
    {
        ProductsExportRequestedMessage message = new(
            tenantContext.TenantId,
            tenantContext.UserId,
            request.Format,
            DateTime.UtcNow);

        await messagePublisher.PublishAsync(ProductsExportRequestedMessage.QueueName, message, cancellationToken);

        return Result.Success();
    }
}
