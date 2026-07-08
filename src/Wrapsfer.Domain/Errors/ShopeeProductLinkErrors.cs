using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class ShopeeProductLinkErrors
{
    public static readonly Error NotFound = new(
        "ShopeeProductLink.NotFound",
        "The Shopee product link was not found",
        ErrorType.NotFound);

    public static readonly Error AlreadyLinked = new(
        "ShopeeProductLink.AlreadyLinked",
        "This Shopee listing is already linked to a local product",
        ErrorType.Conflict);

    public static readonly Error ProductAlreadyLinked = new(
        "ShopeeProductLink.ProductAlreadyLinked",
        "This product is already linked to a Shopee listing",
        ErrorType.Conflict);

    public static readonly Error ProductNotFound = new(
        "ShopeeProductLink.ProductNotFound",
        "The product was not found",
        ErrorType.NotFound);

    public static readonly Error ProductInactive = new(
        "ShopeeProductLink.ProductInactive",
        "Inactive products cannot be linked to Shopee",
        ErrorType.Validation);

    public static readonly Error BundleProductNotAllowed = new(
        "ShopeeProductLink.BundleProductNotAllowed",
        "Bundle products cannot be linked to Shopee",
        ErrorType.Validation);

    public static readonly Error ConnectionNotFound = new(
        "ShopeeProductLink.ConnectionNotFound",
        "No Shopee shop is linked to this tenant",
        ErrorType.NotFound);

    public static readonly Error InvalidTenantId = new(
        "ShopeeProductLink.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidItemId = new(
        "ShopeeProductLink.InvalidItemId",
        "Shopee item ID must be a positive number",
        ErrorType.Validation);

    public static readonly Error InvalidModelId = new(
        "ShopeeProductLink.InvalidModelId",
        "Shopee model ID must be zero or greater",
        ErrorType.Validation);

    public static readonly Error ItemNotFoundInShop = new(
        "ShopeeProductLink.ItemNotFoundInShop",
        "The Shopee item was not found in the linked shop",
        ErrorType.NotFound);

    public static readonly Error ModelMismatch = new(
        "ShopeeProductLink.ModelMismatch",
        "The Shopee model selection does not match the item",
        ErrorType.Validation);

    public static readonly Error ItemFetchFailed = new(
        "ShopeeProductLink.ItemFetchFailed",
        "Shopee item details could not be fetched",
        ErrorType.Failure);

    public static readonly Error StockPushFailed = new(
        "ShopeeProductLink.StockPushFailed",
        "Shopee stock could not be updated",
        ErrorType.Failure);

    public static readonly Error AuthFailed = new(
        "Shopee.AuthFailed",
        "Shopee authentication failed",
        ErrorType.Failure);
}
