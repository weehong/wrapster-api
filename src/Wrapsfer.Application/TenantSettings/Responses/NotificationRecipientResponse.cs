namespace Wrapsfer.Application.TenantSettings.Responses;

public sealed record NotificationRecipientResponse(
    Guid Id,
    string Email,
    bool IsActive);
