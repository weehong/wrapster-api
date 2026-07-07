namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeTokenGrant(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds);
