namespace Wrapsfer.Application.Waybills.Responses;

public sealed record WaybillItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductBarcode,
    string? ProductName,
    int Quantity);
