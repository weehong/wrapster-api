using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

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
