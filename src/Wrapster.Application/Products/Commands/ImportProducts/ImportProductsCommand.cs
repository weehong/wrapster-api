using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Products.Responses;

namespace Wrapster.Application.Products.Commands.ImportProducts;

public sealed record ImportProductsCommand(Stream File, ProductFileFormat Format)
    : ICommand<ProductImportResult>;
