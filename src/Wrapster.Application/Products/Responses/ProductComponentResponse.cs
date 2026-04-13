namespace Wrapster.Application.Products.Responses;

public sealed record ProductComponentResponse(
    Guid Id,
    Guid ChildProductId,
    string ChildProductName,
    string ChildProductBarcode,
    int Quantity);
