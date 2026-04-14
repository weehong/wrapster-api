using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.RequestWaybillsExport;

public sealed record RequestWaybillsExportCommand(ProductFileFormat Format) : ICommand;
