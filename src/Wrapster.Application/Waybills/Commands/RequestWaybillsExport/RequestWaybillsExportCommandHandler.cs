using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Abstractions.Queue;
using Wrapster.Application.Waybills.Messaging;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Waybills.Commands.RequestWaybillsExport;

internal sealed class RequestWaybillsExportCommandHandler(
    IMessagePublisher messagePublisher,
    ITenantContext tenantContext) : ICommandHandler<RequestWaybillsExportCommand>
{
    public async Task<Result> Handle(RequestWaybillsExportCommand request, CancellationToken cancellationToken)
    {
        WaybillsExportRequestedMessage message = new(
            tenantContext.TenantId,
            tenantContext.UserId,
            request.Format,
            DateTime.UtcNow);

        await messagePublisher.PublishAsync(WaybillsExportRequestedMessage.QueueName, message, cancellationToken);

        return Result.Success();
    }
}
