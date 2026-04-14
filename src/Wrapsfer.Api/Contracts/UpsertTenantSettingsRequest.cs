namespace Wrapsfer.Api.Contracts;

public sealed record UpsertTenantSettingsRequest(
    int? DefaultLowStockThreshold = null,
    bool ClearDefaultLowStockThreshold = false);
