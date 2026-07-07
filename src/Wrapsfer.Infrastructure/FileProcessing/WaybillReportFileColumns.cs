namespace Wrapsfer.Infrastructure.FileProcessing;

internal static class WaybillReportFileColumns
{
    public static readonly IReadOnlyList<string> AllInOrder =
    [
        "tenantId",
        "packagingDate",
        "waybillNumber",
        "status",
        "cancellationReason",
        "packedAtUtc",
        "handedOffAtUtc",
        "cancelledAtUtc",
        "createdAtUtc",
        "createdBy",
        "productBarcode",
        "productName",
        "quantity"
    ];
}
