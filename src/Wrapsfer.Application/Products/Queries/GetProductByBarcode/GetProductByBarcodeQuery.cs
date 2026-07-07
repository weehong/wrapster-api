using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products.Responses;

namespace Wrapsfer.Application.Products.Queries.GetProductByBarcode;

public sealed record GetProductByBarcodeQuery(string Barcode) : IQuery<ProductResponse>;
