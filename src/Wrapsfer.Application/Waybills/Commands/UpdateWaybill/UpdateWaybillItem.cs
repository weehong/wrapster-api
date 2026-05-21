namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybill;

public sealed record UpdateWaybillItem(Guid? ProductId, string? Barcode, int Quantity);
