using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

public sealed class ShopeeShopConnection : AuditableEntity
{
    /// <summary>
    /// Shopee refresh tokens are valid for 30 days from issuance and are single-use;
    /// each refresh returns a new pair. The platform does not return this lifetime,
    /// so it is tracked here to know when a partner must re-authorize.
    /// </summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private const int ShopNameMaxLength = 256;
    private const int RegionMaxLength = 16;

    private ShopeeShopConnection()
    {
    }

    public string TenantId { get; private set; } = default!;
    public long ShopId { get; private set; }
    public string? ShopName { get; private set; }
    public string? Region { get; private set; }
    public string AccessToken { get; private set; } = default!;
    public string RefreshToken { get; private set; } = default!;
    public DateTime AccessTokenExpiresAt { get; private set; }
    public DateTime RefreshTokenExpiresAt { get; private set; }
    public DateTime LinkedAt { get; private set; }
    public string? LinkedBy { get; private set; }

    public static Result<ShopeeShopConnection> Create(
        string tenantId,
        long shopId,
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt,
        DateTime refreshTokenExpiresAt,
        DateTime linkedAt,
        string? linkedBy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<ShopeeShopConnection>.Failure(ShopeeShopConnectionErrors.InvalidTenantId);
        }

        if (shopId <= 0)
        {
            return Result<ShopeeShopConnection>.Failure(ShopeeShopConnectionErrors.InvalidShopId);
        }

        Result tokenValidation = ValidateTokens(
            accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt);
        if (tokenValidation.IsFailure)
        {
            return Result<ShopeeShopConnection>.Failure(tokenValidation.Error);
        }

        ShopeeShopConnection connection = new()
        {
            TenantId = tenantId,
            ShopId = shopId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            LinkedAt = linkedAt,
            LinkedBy = linkedBy
        };

        return Result<ShopeeShopConnection>.Success(connection);
    }

    public Result UpdateTokens(
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt,
        DateTime refreshTokenExpiresAt)
    {
        Result tokenValidation = ValidateTokens(
            accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt);
        if (tokenValidation.IsFailure)
        {
            return tokenValidation;
        }

        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
        return Result.Success();
    }

    public void UpdateShopProfile(string? shopName, string? region)
    {
        ShopName = Truncate(shopName, ShopNameMaxLength);
        Region = Truncate(region, RegionMaxLength);
    }

    private static Result ValidateTokens(
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt,
        DateTime refreshTokenExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Result.Failure(ShopeeShopConnectionErrors.InvalidAccessToken);
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result.Failure(ShopeeShopConnectionErrors.InvalidRefreshToken);
        }

        if (accessTokenExpiresAt == default || refreshTokenExpiresAt == default)
        {
            return Result.Failure(ShopeeShopConnectionErrors.InvalidTokenExpiry);
        }

        return Result.Success();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
