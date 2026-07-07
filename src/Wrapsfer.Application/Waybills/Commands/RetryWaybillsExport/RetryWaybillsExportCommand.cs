using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.RetryWaybillsExport;

public sealed record RetryWaybillsExportCommand(Guid JobId) : ICommand;
