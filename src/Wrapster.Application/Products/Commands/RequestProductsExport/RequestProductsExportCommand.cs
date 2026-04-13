using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Products.Commands.RequestProductsExport;

public sealed record RequestProductsExportCommand(ProductFileFormat Format) : ICommand;
