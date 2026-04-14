using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

public sealed class TenantSettings : AuditableEntity
{
    private readonly List<NotificationRecipient> _recipients = [];

    private TenantSettings()
    {
    }

    public string TenantId { get; private set; } = default!;
    public int? DefaultLowStockThreshold { get; private set; }

    public IReadOnlyCollection<NotificationRecipient> Recipients => _recipients.AsReadOnly();

    public static Result<TenantSettings> Create(string tenantId, int? defaultLowStockThreshold = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<TenantSettings>.Failure(TenantSettingsErrors.InvalidTenantId);
        }

        if (defaultLowStockThreshold.HasValue && defaultLowStockThreshold.Value < 0)
        {
            return Result<TenantSettings>.Failure(TenantSettingsErrors.InvalidThreshold);
        }

        return Result<TenantSettings>.Success(new TenantSettings
        {
            TenantId = tenantId,
            DefaultLowStockThreshold = defaultLowStockThreshold
        });
    }

    public Result SetDefaultLowStockThreshold(int? threshold)
    {
        if (threshold.HasValue && threshold.Value < 0)
        {
            return Result.Failure(TenantSettingsErrors.InvalidThreshold);
        }

        DefaultLowStockThreshold = threshold;
        return Result.Success();
    }

    public Result<NotificationRecipient> AddRecipient(string email)
    {
        string trimmed = email?.Trim() ?? string.Empty;

        if (_recipients.Any(r => string.Equals(r.Email, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<NotificationRecipient>.Failure(TenantSettingsErrors.RecipientAlreadyExists);
        }

        Result<NotificationRecipient> recipientResult = NotificationRecipient.Create(TenantId, Id, trimmed);
        if (recipientResult.IsFailure)
        {
            return recipientResult;
        }

        _recipients.Add(recipientResult.Value);
        return recipientResult;
    }

    public Result RemoveRecipient(Guid recipientId)
    {
        NotificationRecipient? recipient = _recipients.FirstOrDefault(r => r.Id == recipientId);
        if (recipient is null)
        {
            return Result.Failure(TenantSettingsErrors.RecipientNotFound);
        }

        _recipients.Remove(recipient);
        return Result.Success();
    }

    public Result SetRecipientActive(Guid recipientId, bool isActive)
    {
        NotificationRecipient? recipient = _recipients.FirstOrDefault(r => r.Id == recipientId);
        if (recipient is null)
        {
            return Result.Failure(TenantSettingsErrors.RecipientNotFound);
        }

        recipient.SetActive(isActive);
        return Result.Success();
    }
}
