namespace Wrapster.Api.Contracts;

public sealed record UpsertTenantSettingsRequest(
    int? DefaultLowStockThreshold = null,
    bool ClearDefaultLowStockThreshold = false);
