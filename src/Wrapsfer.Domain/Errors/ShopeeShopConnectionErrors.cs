using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class ShopeeShopConnectionErrors
{
    public static readonly Error NotFound = new(
        "ShopeeShopConnection.NotFound",
        "No Shopee shop is linked to this tenant",
        ErrorType.NotFound);

    public static readonly Error PartnerTenantNotFound = new(
        "ShopeeShopConnection.PartnerTenantNotFound",
        "The partner tenant was not found",
        ErrorType.NotFound);

    public static readonly Error AlreadyLinked = new(
        "ShopeeShopConnection.AlreadyLinked",
        "A Shopee shop is already linked to this tenant. Unlink it before linking another shop",
        ErrorType.Conflict);

    public static readonly Error NotConfigured = new(
        "ShopeeShopConnection.NotConfigured",
        "The Shopee integration is not configured. Contact support",
        ErrorType.Failure);

    public static readonly Error AuthorizationExchangeFailed = new(
        "ShopeeShopConnection.AuthorizationExchangeFailed",
        "Shopee rejected the authorization. Please retry linking the shop",
        ErrorType.Failure);

    public static readonly Error ShopProfileFetchFailed = new(
        "ShopeeShopConnection.ShopProfileFetchFailed",
        "Shopee did not return the shop profile",
        ErrorType.Failure);

    public static readonly Error TokenRefreshFailed = new(
        "ShopeeShopConnection.TokenRefreshFailed",
        "Shopee rejected the token refresh",
        ErrorType.Failure);

    public static readonly Error InvalidRedirectUrl = new(
        "ShopeeShopConnection.InvalidRedirectUrl",
        "The redirect URL must be an absolute http(s) URL",
        ErrorType.Validation);

    public static readonly Error InvalidTenantId = new(
        "ShopeeShopConnection.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidShopId = new(
        "ShopeeShopConnection.InvalidShopId",
        "Shop ID must be a positive number",
        ErrorType.Validation);

    public static readonly Error InvalidAccessToken = new(
        "ShopeeShopConnection.InvalidAccessToken",
        "Access token is required",
        ErrorType.Validation);

    public static readonly Error InvalidRefreshToken = new(
        "ShopeeShopConnection.InvalidRefreshToken",
        "Refresh token is required",
        ErrorType.Validation);

    public static readonly Error InvalidTokenExpiry = new(
        "ShopeeShopConnection.InvalidTokenExpiry",
        "Token expiry timestamps are required",
        ErrorType.Validation);

    public static readonly Error InvalidAuthorizationCode = new(
        "ShopeeShopConnection.InvalidAuthorizationCode",
        "Authorization code is required",
        ErrorType.Validation);
}
