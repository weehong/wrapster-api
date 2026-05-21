namespace Wrapsfer.Api.Contracts;

public sealed record UpdateWaybillItemRequest(Guid? ProductId, string? Barcode, int Quantity);
