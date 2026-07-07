namespace Wrapsfer.Api.Contracts;

public sealed record UpdatePurchaseOrderRequest(
    string PoNumber,
    int Quantity);
