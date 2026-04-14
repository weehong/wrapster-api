namespace Wrapsfer.Api.Contracts;

public sealed record CreateWaybillRequest(DateOnly PackagingDate, string WaybillNumber);
