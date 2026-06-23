namespace Wrapsfer.Api.Contracts;

public sealed record CreatePurchaseOrderRequest(
    string PoNumber,
    Guid ProductId,
    int Quantity);
