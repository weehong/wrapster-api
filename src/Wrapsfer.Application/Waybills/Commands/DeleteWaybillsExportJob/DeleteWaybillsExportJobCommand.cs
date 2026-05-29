using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.DeleteWaybillsExportJob;

public sealed record DeleteWaybillsExportJobCommand(Guid JobId) : ICommand;
