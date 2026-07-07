using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Application.Products.Messaging;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Products.Commands.RequestProductsExport;

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
