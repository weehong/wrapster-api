namespace Wrapsfer.Application.TenantSettings.Responses;

public sealed record TenantSettingsResponse(
    string TenantId,
    int? DefaultLowStockThreshold,
    IReadOnlyList<NotificationRecipientResponse> Recipients);
