using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products.Responses;

namespace Wrapsfer.Application.Products.Commands.ImportProducts;

public sealed record ImportProductsCommand(Stream File, ProductFileFormat Format)
    : ICommand<ProductImportResult>;
