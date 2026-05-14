namespace Wrapsfer.Api.Contracts;

public sealed record CreateWaybillItemRequest(string Barcode, int Quantity = 1);
