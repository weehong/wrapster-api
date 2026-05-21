namespace Wrapsfer.Api.Contracts;

public sealed record UpdateWaybillRequest(
    DateOnly PackagingDate,
    string WaybillNumber,
    IReadOnlyList<UpdateWaybillItemRequest> Items);
