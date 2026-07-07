namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeConnectionResponse(
    long ShopId,
    string? ShopName,
    string? Region,
    DateTime LinkedAt,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
