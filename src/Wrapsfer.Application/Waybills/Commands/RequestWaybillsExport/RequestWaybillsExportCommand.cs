using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

public sealed record RequestWaybillsExportCommand(ProductFileFormat Format) : ICommand;
