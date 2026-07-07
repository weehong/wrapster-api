namespace Wrapsfer.Api.Contracts;

public sealed record CompleteShopeeAuthorizationRequest(string Code, long ShopId);
