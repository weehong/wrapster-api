using Wrapster.Application.Waybills.Responses;
using Wrapster.Domain.Entities;

namespace Wrapster.Application.Waybills.Common;

internal static class WaybillResponseMapper
{
    internal static WaybillResponse ToResponse(Waybill waybill, IReadOnlyDictionary<Guid, string> productNamesById)
    {
        List<WaybillItemResponse> itemResponses = waybill.Items
            .Select(i => new WaybillItemResponse(
                i.Id,
                i.ProductId,
                i.ProductBarcode,
                productNamesById.TryGetValue(i.ProductId, out string? name) ? name : null,
                i.Quantity))
            .ToList();

        return new WaybillResponse(
            waybill.Id,
            waybill.PackagingDate,
            waybill.WaybillNumber,
            waybill.Status,
            waybill.CancellationReason,
            waybill.PackedAt,
            waybill.HandedOffAt,
            waybill.CancelledAt,
            waybill.CreatedAt,
            waybill.UpdatedAt,
            waybill.CreatedBy,
            itemResponses);
    }
}
