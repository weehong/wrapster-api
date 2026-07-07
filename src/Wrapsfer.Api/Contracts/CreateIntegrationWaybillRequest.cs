namespace Wrapsfer.Api.Contracts;

public sealed record CreateIntegrationWaybillRequest(
    DateOnly PackagingDate,
    string WaybillNumber,
    IReadOnlyList<CreateIntegrationWaybillItemRequest> Items);
