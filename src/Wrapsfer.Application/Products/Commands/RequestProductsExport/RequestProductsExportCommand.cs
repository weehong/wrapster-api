using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Products.Commands.RequestProductsExport;

public sealed record RequestProductsExportCommand(ProductFileFormat Format) : ICommand;
