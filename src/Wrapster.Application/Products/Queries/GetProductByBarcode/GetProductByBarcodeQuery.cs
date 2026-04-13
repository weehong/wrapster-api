using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Products.Responses;

namespace Wrapster.Application.Products.Queries.GetProductByBarcode;

public sealed record GetProductByBarcodeQuery(string Barcode) : IQuery<ProductResponse>;
