namespace Wrapster.Api.Contracts;

public sealed record AddWaybillItemRequest(string Barcode, int Quantity = 1);
